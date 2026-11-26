"""Download pinned official Windows SDK in bounded ranges and verify SHA512."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor,as_completed
import urllib.request,hashlib,zipfile,time
root=Path(__file__).resolve().parents[1]
url='https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.425/dotnet-sdk-8.0.425-win-x64.zip'
expected='f0b6f15bf6f1a0507205c0cb102ab99e1dee875c4682c8ed94665be1d580186a06b21455e83b3a01a0ff7f4cd887b67420f2e2fe09ed985534a4cea488ae1af9'
size=285157731;chunk=1024*1024
target=root/'tools/sdk.zip';target.parent.mkdir(exist_ok=True)
def fetch(start):
 end=min(start+chunk,size)-1
 for attempt in range(4):
  try:
   opener=urllib.request.build_opener(urllib.request.ProxyHandler({}))
   with opener.open(urllib.request.Request(url,headers={'Range':f'bytes={start}-{end}'}),timeout=30) as response:
    if response.status!=206:raise RuntimeError('Server did not honor bounded range')
    data=response.read()
   if len(data)!=end-start+1:raise RuntimeError('Truncated download')
   return start,data
  except Exception:
   if attempt==3:raise
   time.sleep(attempt+1)
if not target.exists() or hashlib.sha512(target.read_bytes()).hexdigest()!=expected:
 with target.open('wb') as output,ThreadPoolExecutor(max_workers=8) as pool:
  output.truncate(size);futures=[pool.submit(fetch,n) for n in range(0,size,chunk)]
  done=0
  for future in as_completed(futures):
   start,data=future.result();output.seek(start);output.write(data);done+=len(data)
   if done//(32*chunk)!=(done-len(data))//(32*chunk):print(f'SDK {done/size:.0%}',flush=True)
if hashlib.sha512(target.read_bytes()).hexdigest()!=expected:raise RuntimeError('SDK SHA512 mismatch')
with zipfile.ZipFile(target) as archive:archive.extractall(root/'tools/dotnet')
print('SDK 8.0.425 installed locally; SHA512 verified.',flush=True)
