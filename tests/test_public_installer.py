"""Exercise actual installer processes against isolated files; never starts the game."""
import hashlib,json,os,shutil,subprocess,tempfile,unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
SHELL=os.environ.get('DAXIA_TEST_POWERSHELL') or shutil.which('pwsh') or 'powershell.exe'
REQUIRED=['winhttp.dll','BepInEx/core/BepInEx.Unity.IL2CPP.dll','BepInEx/core/0Harmony.dll','BepInEx/interop/Assembly-CSharp.dll']

class InstallerTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        base=Path(self.temp.name);self.game=base/'steamapps/common/WulinSH';self.game.mkdir(parents=True)
        self.pkg=base/'package';self.pkg.mkdir()
        shutil.copy2(ROOT/'scripts/Manage-Mods.ps1',self.pkg)
        shutil.copy2(ROOT/'scripts/Resolve-GamePath.ps1',self.pkg)
        (self.game/'Wulin.exe').write_text('fixture, never executed')
        (self.game/'GameAssembly.dll').write_bytes(b'game-fixture')
        self.steam=base/'steamapps/appmanifest_1948980.acf'
        self.steam.write_text('"AppState" { "buildid" "24794582" }')
        for name in REQUIRED:
            target=self.game/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b'preexisting-runtime')
        self.files=[]
        for mod in ['FastFinish','ExploreCamera','FreeNaming','FiefLibrary']:
            for rel,data in [(f'BepInEx/plugins/DaxiaMods/{mod}.dll',f'{mod}-dll'.encode()),(f'BepInEx/config/local.daxia.{mod.lower()}.cfg',b'[General]\nEnabled = true\n')]:
                target=self.pkg/'payload'/rel;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
                self.files.append(dict(Relative=rel.replace('/','\\'),Group=mod,Hash=hashlib.sha256(data).hexdigest()))
        self.manifest=dict(GameBuild='24794582',GameAssemblyHash=hashlib.sha256(b'game-fixture').hexdigest(),Files=self.files)
        self.write_manifest()
    def write_manifest(self):
        (self.pkg/'payload-manifest.json').write_text(json.dumps(self.manifest))
    def run_installer(self,action='Install',mod='FreeNaming',ok=True):
        # Let each host initialize its own standard modules instead of inheriting PS7 paths through Python.
        child_env={key:value for key,value in os.environ.items() if key.upper()!='PSMODULEPATH'}
        run=subprocess.run([SHELL,'-NoProfile','-ExecutionPolicy','Bypass','-File',str(self.pkg/'Manage-Mods.ps1'),'-GamePath',str(self.game),'-Action',action,'-Mod',mod],capture_output=True,text=True,encoding='utf-8',errors='replace',env=child_env)
        self.assertEqual(run.returncode==0,ok,run.stdout+run.stderr)
        return run
    def test_missing_frontend_refuses_without_writes(self):
        (self.game/'winhttp.dll').unlink();self.run_installer(ok=False)
        self.assertFalse((self.game/'.daxia-mods-store').exists())
    def test_missing_interop_refuses_without_writes(self):
        (self.game/'BepInEx/interop/Assembly-CSharp.dll').unlink();self.run_installer(ok=False)
        self.assertFalse((self.game/'.daxia-mods-store').exists())
    def test_install_uninstall_keeps_frontend_and_user_config(self):
        cfg=self.game/'BepInEx/config/local.daxia.freenaming.cfg';cfg.parent.mkdir(parents=True,exist_ok=True);cfg.write_text('user preference')
        self.run_installer();self.assertTrue((self.game/'BepInEx/plugins/DaxiaMods/FreeNaming.dll').exists())
        self.run_installer('Uninstall');self.assertEqual(cfg.read_text(),'user preference')
        self.assertEqual((self.game/'winhttp.dll').read_bytes(),b'preexisting-runtime')
    def test_both_selects_only_two_plugins(self):
        self.run_installer(mod='Both')
        self.assertTrue((self.game/'BepInEx/plugins/DaxiaMods/FastFinish.dll').exists())
        self.assertTrue((self.game/'BepInEx/plugins/DaxiaMods/ExploreCamera.dll').exists())
        self.assertFalse((self.game/'BepInEx/plugins/DaxiaMods/FreeNaming.dll').exists())
    def test_wrong_build_and_binary_refuse(self):
        self.steam.write_text('"buildid" "0"');self.run_installer(ok=False)
        self.steam.write_text('"buildid" "24794582"');(self.game/'GameAssembly.dll').write_bytes(b'wrong');self.run_installer(ok=False)
    def test_tampered_payload_refuses_before_receipt(self):
        (self.pkg/'payload/BepInEx/plugins/DaxiaMods/FreeNaming.dll').write_bytes(b'tampered')
        self.run_installer(ok=False);self.assertFalse((self.game/'.daxia-mods-store').exists())
    def test_same_name_backup_and_old_runtime_receipt_survive(self):
        prior=self.game/'BepInEx/plugins/DaxiaMods/FreeNaming.dll';prior.parent.mkdir(parents=True,exist_ok=True);prior.write_bytes(b'prior-plugin')
        store=self.game/'.daxia-mods-store';store.mkdir()
        runtime=dict(Relative='winhttp.dll',Group='Runtime',InstalledHash=hashlib.sha256(b'preexisting-runtime').hexdigest(),Backup=None,BackupHash=None)
        (store/'receipt.json').write_text(json.dumps(dict(Schema=1,GamePath=str(self.game),Files=[runtime])))
        self.run_installer();self.run_installer('Uninstall')
        self.assertEqual(prior.read_bytes(),b'prior-plugin')
        self.assertEqual((self.game/'winhttp.dll').read_bytes(),b'preexisting-runtime')
        self.assertIn(runtime,json.loads((store/'receipt.json').read_text(encoding='utf-8-sig'))['Files'])
    def test_runtime_payload_is_rejected(self):
        target=self.pkg/'payload/winhttp.dll';target.write_bytes(b'new-runtime')
        self.manifest['Files'].append(dict(Relative='winhttp.dll',Group='Runtime',Hash=hashlib.sha256(b'new-runtime').hexdigest()));self.write_manifest()
        self.run_installer(ok=False);self.assertEqual((self.game/'winhttp.dll').read_bytes(),b'preexisting-runtime')
    def test_running_game_refuses_before_receipt(self):
        # A renamed Windows utility exercises Get-Process Wulin without launching the game.
        fake=Path(self.temp.name)/'Wulin.exe'
        shutil.copy2(Path(os.environ['SystemRoot'])/'System32/ping.exe',fake)
        process=subprocess.Popen([str(fake),'-t','127.0.0.1'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            self.assertIsNone(process.poll())
            self.run_installer(ok=False)
            self.assertFalse((self.game/'.daxia-mods-store').exists())
        finally:
            process.terminate();process.wait(timeout=10)
    def test_last_plugin_uninstall_preserves_legacy_runtime_receipt(self):
        store=self.game/'.daxia-mods-store';store.mkdir()
        runtime=dict(Relative='winhttp.dll',Group='Runtime',InstalledHash=hashlib.sha256(b'preexisting-runtime').hexdigest(),Backup=None,BackupHash=None)
        (store/'receipt.json').write_text(json.dumps(dict(Schema=1,GamePath=str(self.game),Files=[runtime])))
        self.run_installer();self.run_installer('Uninstall')
        self.assertEqual((self.game/'winhttp.dll').read_bytes(),b'preexisting-runtime')
        self.assertIn(runtime,json.loads((store/'receipt.json').read_text(encoding='utf-8-sig'))['Files'])
    def test_manifest_path_traversal_is_rejected(self):
        self.manifest['Files'][0]['Relative']='..\\outside.dll';self.write_manifest()
        self.run_installer(ok=False);self.assertFalse((self.game/'.daxia-mods-store').exists())

if __name__=='__main__':unittest.main()
