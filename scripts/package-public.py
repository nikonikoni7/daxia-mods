"""Build a plugin-only release from explicit authored inputs, never historical outputs."""
from pathlib import Path
import argparse,hashlib,json,shutil,tempfile,zipfile

ROOT=Path(__file__).resolve().parents[1]
GAME_HASH='F8393E44423442526DFFBF3AF52F8CCD56FB33769D97E2C7161EBC49D68B3564'

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

def package(plugin_dir,output):
    mods=json.loads((ROOT/'mods.json').read_text(encoding='utf-8'))
    for mod in mods:
        if not (plugin_dir/(mod['name']+'.dll')).is_file():
            raise ValueError('Missing compiled plugin: '+mod['name'])
    archive=output/'DaxiaMods-v1.0.1-Build24794582.zip'
    if archive.exists() or archive.with_suffix('.zip.sha256').exists():
        raise FileExistsError('Release already exists; choose a new output directory')
    output.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='daxia-release-') as temporary:
        stage=Path(temporary)
        def copy(source,relative):
            target=stage/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
        for name in ['README.md','LICENSE','THIRD_PARTY_NOTICES.md','mods.json','docs/DEVELOPMENT.md','docs/VALIDATION.md']:
            copy(ROOT/name,name)
        copy(ROOT/'scripts/Manage-Mods.ps1','Manage-Mods.ps1')
        copy(ROOT/'scripts/Resolve-GamePath.ps1','Resolve-GamePath.ps1')
        tower_hash=digest(plugin_dir/'TowerRecovery.dll')
        installer=(ROOT/'scripts/Install-TowerRecovery.ps1').read_text(encoding='utf-8-sig').replace('@TOWER_RECOVERY_SHA256@',tower_hash)
        (stage/'Install-TowerRecovery.ps1').write_text(installer,encoding='utf-8-sig')
        entries=[]
        for mod in mods:
            name=mod['name']
            copy(ROOT/f'docs/mods/{name}.md',f'docs/mods/{name}.md')
            if name=='TowerRecovery':
                copy(plugin_dir/(name+'.dll'),name+'.dll')
                copy(ROOT/f'config/defaults/local.daxia.{name.lower()}.cfg',f'defaults/local.daxia.{name.lower()}.cfg')
                continue
            for source,relative in [
                (plugin_dir/(name+'.dll'),f'BepInEx/plugins/DaxiaMods/{name}.dll'),
                (ROOT/f'config/defaults/local.daxia.{name.lower()}.cfg',f'BepInEx/config/local.daxia.{name.lower()}.cfg')]:
                copy(source,'payload/'+relative)
                entries.append(dict(Relative=relative.replace('/','\\'),Group=name,Hash=digest(source)))
        manifest=dict(GameBuild='24794582',GameAssemblyHash=GAME_HASH,Files=entries)
        (stage/'payload-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
        checksums=''.join(f'{digest(p)}  {p.relative_to(stage).as_posix()}\n' for p in sorted(stage.rglob('*')) if p.is_file())
        (stage/'SHA256SUMS').write_text(checksums,encoding='utf-8')
        # Write the completed archive only after every source has been staged successfully.
        with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_DEFLATED) as z:
            for path in sorted(stage.rglob('*')):
                if path.is_file():z.write(path,path.relative_to(stage).as_posix())
    archive.with_suffix('.zip.sha256').write_text(f'{digest(archive)}  {archive.name}\n',encoding='ascii')
    print(archive)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plugin-dir',type=Path,default=ROOT/'outputs/DaxiaMods/plugins')
    parser.add_argument('--output',type=Path,default=ROOT/'outputs/releases')
    args=parser.parse_args()
    package(args.plugin_dir,args.output)
