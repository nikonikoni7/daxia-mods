"""Steam library discovery uses temporary fixtures, independent of this PC's registry."""
import base64,os,shutil,subprocess,tempfile,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
SHELL=os.environ.get('DAXIA_TEST_POWERSHELL') or shutil.which('pwsh') or 'powershell.exe'
class DiscoveryTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.base=Path(self.temp.name)
    def game(self,library,folder='WulinSH'):
        game=library/'steamapps/common'/folder;game.mkdir(parents=True)
        (game/'Wulin.exe').write_text('fixture only')
        (library/'steamapps/appmanifest_1948980.acf').write_text('"AppState" { "installdir" "'+folder+'" }',encoding='utf-8')
        return game
    def resolve(self,roots,explicit=None,ok=True):
        helper=ROOT/'scripts/Resolve-GamePath.ps1'
        self.assertTrue(helper.exists(),'Steam discovery helper has not been implemented')
        quote=lambda value:"'"+str(value).replace("'","''")+"'"
        command=f"$ErrorActionPreference='Stop'; [Console]::OutputEncoding=New-Object Text.UTF8Encoding; . {quote(helper)}; Resolve-WulinGamePath -SteamRoots @({','.join(map(quote,roots))})"
        if explicit is not None:command+=' -GamePath '+quote(explicit)
        command+=' | ConvertTo-Json -Compress'
        run=subprocess.run([SHELL,'-NoProfile','-EncodedCommand',base64.b64encode(command.encode('utf-16-le')).decode()],capture_output=True,text=True,encoding='utf-8',errors='replace')
        self.assertEqual(run.returncode==0,ok,run.stdout+run.stderr)
        if ok:
            import json
            return Path(json.loads(run.stdout.strip()))
    def test_main_library(self):
        root=self.base/'Steam';game=self.game(root);self.assertEqual(self.resolve([root]),game)
    def test_other_drive_library_spaces_unicode_and_custom_folder(self):
        steam=self.base/'Steam';(steam/'steamapps').mkdir(parents=True)
        library=self.base/'游戏库 with spaces';game=self.game(library,'游戏目录 with spaces')
        escaped=str(library).replace('\\','\\\\')
        (steam/'steamapps/libraryfolders.vdf').write_text('"libraryfolders" { "1" { "path" "'+escaped+'" } }',encoding='utf-8')
        self.assertEqual(self.resolve([steam]),game)
    def test_explicit_path_has_priority(self):
        game=self.game(self.base/'chosen');self.game(self.base/'other')
        self.assertEqual(self.resolve([self.base/'other'],game),game)
    def test_multiple_installs_require_explicit_choice(self):
        self.game(self.base/'first');self.game(self.base/'second')
        self.resolve([self.base/'first',self.base/'second'],ok=False)
    def test_no_install_refuses(self):self.resolve([],ok=False)
    def test_explicit_missing_game_refuses(self):self.resolve([],self.base/'missing',ok=False)
    def test_duplicate_library_is_one_install(self):
        root=self.base/'Steam';game=self.game(root);self.assertEqual(self.resolve([root,root]),game)
if __name__=='__main__':unittest.main()
