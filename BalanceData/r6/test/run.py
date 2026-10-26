import json,sys,subprocess
code=open(sys.argv[1],encoding='utf-8').read()
a=json.dumps({"action":"execute","code":code})
open('BalanceData/r6/test/_args.json','w',encoding='utf-8').write(a)
print(subprocess.run([sys.executable,'-X','utf8','Tools/umcp.py','call','execute_code','@BalanceData/r6/test/_args.json'],capture_output=True,text=True,encoding='utf-8').stdout)
