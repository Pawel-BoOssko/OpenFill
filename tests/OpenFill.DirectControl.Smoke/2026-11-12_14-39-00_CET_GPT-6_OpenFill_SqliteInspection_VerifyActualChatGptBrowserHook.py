import sqlite3
import sys
import json
db = sqlite3.connect(sys.argv[1])
rows = db.execute("SELECT chat_id, title, url, first_access_utc, last_access_utc FROM chat_visits ORDER BY last_access_utc DESC LIMIT 20").fetchall()
print(json.dumps(rows, ensure_ascii=False, indent=2))

