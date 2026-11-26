"""Offline recovery regression using generated game/save fixtures only."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile

script = Path(__file__).resolve().parents[3] / 'scripts/recover-chest-memo.py'
work = script.parents[1] / 'work'
work.mkdir(exist_ok=True)
with tempfile.TemporaryDirectory(prefix='ChestMemoRecovery-', dir=work) as temp:
    root = Path(temp)
    assert root.resolve().is_relative_to(work.resolve())
    game, slot, evidence = root / 'game', root / 'saves/account/QuickSave0', root / 'evidence'
    for folder in (game, slot, evidence):
        folder.mkdir(parents=True)
    (game / 'Wulin.exe').write_bytes(b'fixture only')
    save = slot / 'SaveObjectGame.save'
    save.write_bytes(b'original save bytes')
    record = {'MapId': 1, 'SaveId': 3, 'TemplateId': 0, 'X': 4, 'Y': 5, 'Z': 0, 'MapName': '地图', 'Routes': [{'Keys': [{'Id': 101, 'Count': 2, 'Name': '钥匙'}], 'HasOtherConditions': False}]}
    older = dict(record, SaveId=4)
    for name, records in [('pending-example.json', [record]), ('pending-example.json.bak', [record, older])]:
        (evidence / name).write_text(json.dumps({'Schema': 1, 'Records': records}), encoding='utf-8')
    originals = {p.name: p.read_bytes() for p in evidence.iterdir()}
    command = [sys.executable, str(script), '--game-path', str(game), '--slot', str(slot), '--evidence', str(evidence)]
    preview = subprocess.run(command, capture_output=True)
    assert preview.returncode == 0, preview.stderr
    memo = game / 'BepInEx/config/ChestMemo'
    assert not memo.exists()
    print('PASS: recovery preview writes no plugin files')
    result = subprocess.run(command + ['--apply'], capture_output=True)
    assert result.returncode == 0, result.stderr
    snapshots = list(memo.glob('*.json'))
    assert len(snapshots) == 2 and all(len(json.loads(p.read_text(encoding='utf-8'))['Records']) == 2 for p in snapshots)
    print('PASS: recovery merges primary and backup with deduplication into selected slot')
    assert len(list(memo.glob('*.json.bak'))) == 2 and len(list(memo.glob('recovery-backup-*/recovery-report.json'))) == 1
    assert {p.name: p.read_bytes() for p in evidence.iterdir()} == originals
    assert save.read_bytes() == b'original save bytes'
    print('PASS: recovery preserves native save, evidence and record backups')
    before = {p.name: hashlib.sha256(p.read_bytes()).digest() for p in memo.glob('*.json')}
    rejected = subprocess.run(command + ['--apply'], capture_output=True)
    assert rejected.returncode != 0
    assert before == {p.name: hashlib.sha256(p.read_bytes()).digest() for p in memo.glob('*.json')}
    print('PASS: recovery refuses to overwrite existing fingerprinted records')
print('ChestMemo recovery checks passed: 4')
