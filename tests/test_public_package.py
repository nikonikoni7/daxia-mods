"""The release must be a fresh whitelist, never a copy of local delivery material."""
import hashlib,json,subprocess,sys,tempfile,unittest,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
NAMES={'FastFinish','ExploreCamera','ChestMemo','CompanionTraining','PerfectRolls','FreeNaming','FiefReuse','FiefPositionFix','ItemSpawner','AuctionRefresh','AuctionSkyLantern','BountyRefresh','QuestTracking','CompanionTravel','AnswerHints','FusionRename','KungfuOrder','FiefLibrary','TowerRecovery'}
class PackageTests(unittest.TestCase):
    def test_release_contains_only_selected_plugins_and_verifiable_manifest(self):
        with tempfile.TemporaryDirectory() as tmp:
            base=Path(tmp);plugins=base/'plugins';plugins.mkdir();output=base/'release';output.mkdir()
            for name in NAMES:(plugins/(name+'.dll')).write_bytes(name.encode())
            (plugins/'Unrelated.dll').write_bytes(b'private')
            (output/'private-history.txt').write_text('must not be packaged')
            run=subprocess.run([sys.executable,str(ROOT/'scripts/package-public.py'),'--plugin-dir',str(plugins),'--output',str(output)],capture_output=True,text=True)
            self.assertEqual(run.returncode,0,run.stdout+run.stderr)
            archive=next(output.glob('*.zip'))
            with zipfile.ZipFile(archive) as z:
                names=z.namelist();dlls={Path(p).stem for p in names if p.endswith('.dll')}
                self.assertEqual(dlls,NAMES)
                self.assertFalse(any('private' in p or 'RuntimeProbe' in p or 'interop/' in p or 'core/' in p for p in names))
                self.assertIn('LICENSE',names);self.assertIn('THIRD_PARTY_NOTICES.md',names)
                manifest=json.loads(z.read('payload-manifest.json'))
                self.assertFalse(any(f['Group']=='Runtime' for f in manifest['Files']))
                for f in manifest['Files']:
                    self.assertEqual(hashlib.sha256(z.read('payload/'+f['Relative'].replace('\\','/'))).hexdigest().upper(),f['Hash'])
            expected=archive.with_suffix('.zip.sha256').read_text().split()[0]
            self.assertEqual(hashlib.sha256(archive.read_bytes()).hexdigest(),expected.lower())
    def test_missing_plugin_fails_without_archive(self):
        with tempfile.TemporaryDirectory() as tmp:
            base=Path(tmp);(base/'plugins').mkdir()
            run=subprocess.run([sys.executable,str(ROOT/'scripts/package-public.py'),'--plugin-dir',str(base/'plugins'),'--output',str(base/'release')],capture_output=True,text=True)
            self.assertNotEqual(run.returncode,0)
            self.assertFalse(list((base/'release').glob('*.zip')))
if __name__=='__main__':unittest.main()
