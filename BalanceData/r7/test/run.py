import json,sys,subprocess,os
code=open(sys.argv[1],encoding='utf-8').read()
os.makedirs('BalanceData/r7/test',exist_ok=True)
open('BalanceData/r7/test/_args.json','w',encoding='utf-8').write(json.dumps({"action":"execute","code":code}))
r=subprocess.run([sys.executable,'-X','utf8','Tools/umcp.py','call','execute_code','@BalanceData/r7/test/_args.json'],capture_output=True,text=True,encoding='utf-8',stdin=subprocess.DEVNULL)
print(r.stdout[-5000:]); print(r.stderr[-1500:])
