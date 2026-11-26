"""Explicit offline repair of legacy pending records, confined to plugin data files.

The selected slot must be supplied by the operator: pending records have no slot
identity and must never be imported automatically into every save or new game.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
from datetime import datetime


def digest_slot(slot):
    files = sorted(slot.glob('*.save'), key=lambda p: p.name.upper())
    if not files:
        raise ValueError('目标存档没有正式.save文件')
    content = hashlib.sha256()
    for path in files:
        content.update(path.name.encode('utf-8'))
        with path.open('rb') as stream:
            for chunk in iter(lambda: stream.read(65536), b''):
                content.update(chunk)
    prefix = hashlib.sha256(str(slot).upper().encode('utf-8')).hexdigest().upper()
    return prefix + '-' + content.hexdigest().upper()


def records_from_evidence(evidence):
    records = {}
    sources = sorted([*evidence.glob('pending-*.json'), *evidence.glob('pending-*.json.bak')], key=lambda p: p.stat().st_mtime_ns)
    if not sources:
        raise ValueError('没有可恢复的pending记录文件')
    for source in sources:
        data = json.loads(source.read_text(encoding='utf-8-sig'))
        if data.get('Schema') != 1 or not isinstance(data.get('Records'), list):
            raise ValueError('备忘录格式不支持')
        for record in data['Records']:
            if not record.get('Routes') or not isinstance(record.get('MapName'), str):
                raise ValueError('记录不完整，停止恢复')
            for route in record['Routes']:
                for key in route['Keys']:
                    if key['Id'] <= 0 or key['Count'] <= 0 or not isinstance(key['Name'], str):
                        raise ValueError('钥匙条件不完整')
            key = (record['MapId'], record['SaveId']) if record['SaveId'] > 0 else (record['MapId'], 0, record['TemplateId'], record['X'], record['Y'])
            records[key] = record
    return {'Schema': 1, 'Records': list(records.values())}, sources


def reject_links(path):
    for part in [path, *path.parents]:
        if part.exists() and (part.is_symlink() or getattr(part.stat(), 'st_file_attributes', 0) & 0x400):
            raise ValueError('拒绝链接或重解析目录')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-path', type=Path, required=True)
    parser.add_argument('--slot', type=Path, required=True)
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    game = args.game_path.absolute()
    slot = args.slot.absolute()
    reject_links(game)
    reject_links(slot)
    if not (game / 'Wulin.exe').is_file():
        raise ValueError('游戏目录不正确')
    book, sources = records_from_evidence(args.evidence)
    identity = digest_slot(slot)
    memo = game / 'BepInEx/config/ChestMemo'
    reject_links(memo)
    targets = [memo / (identity + ending) for ending in ('.json', '.working.json')]
    # Existing fingerprinted data takes precedence; never overwrite it with a guessed repair.
    for target in targets:
        reject_links(target)
        if target.exists() or Path(str(target) + '.bak').exists():
            raise ValueError('目标存档已经有记录，停止覆盖；请人工合并')
    report = {'Records': len(book['Records']), 'Maps': [r['MapName'] for r in book['Records']], 'Fingerprint': identity, 'Applied': args.apply}
    if args.apply:
        process = subprocess.run(['powershell.exe', '-NoProfile', '-Command', 'if (Get-Process -Name Wulin -ErrorAction SilentlyContinue) { exit 1 }'], capture_output=True)
        if process.returncode:
            raise ValueError('请先退出游戏再恢复')
        backup = memo / ('recovery-backup-' + datetime.now().strftime('%Y%m%d-%H%M%S'))
        backup.mkdir(parents=True, exist_ok=False)
        for source in sources:
            shutil.copy2(source, backup / source.name)
        payload = json.dumps(book, ensure_ascii=False, indent=2)
        # Verify the immutable source save fingerprint immediately before committing plugin data.
        if digest_slot(slot) != identity:
            raise ValueError('恢复过程中原版存档发生变化，停止写入')
        for target in targets:
            tmp = Path(str(target) + '.tmp')
            tmp.write_text(payload, encoding='utf-8')
            os.replace(tmp, target)
            shutil.copy2(target, Path(str(target) + '.bak'))
        (backup / 'recovery-report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False))


if __name__ == '__main__':
    main()
