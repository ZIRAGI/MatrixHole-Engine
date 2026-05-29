import json, re

with open('wwwroot/js/i18n.js', 'r', encoding='utf-8') as f:
    data = f.read()

# Find LOCALE_DATA = { ... };
start = data.find('const LOCALE_DATA = {')
if start == -1:
    print("Could not find LOCALE_DATA")
    exit(1)

# Find the matching closing brace for the outer object
# After the start, we need to find the first "  };" that closes LOCALE_DATA
brace_start = data.find('{', start)
brace_count = 0
end = brace_start
for i, ch in enumerate(data[brace_start:], brace_start):
    if ch == '{':
        brace_count += 1
    elif ch == '}':
        brace_count -= 1
        if brace_count == 0:
            end = i
            break

obj_str = data[brace_start:end+1]
try:
    obj = json.loads(obj_str)
    print("Valid JSON: YES")
    print("EN keys:", len(obj.get('en', {})))
    print("RU keys:", len(obj.get('ru', {})))
    print("PL keys:", len(obj.get('pl', {})))
    en_keys = set(obj['en'].keys())
    ru_keys = set(obj['ru'].keys())
    pl_keys = set(obj['pl'].keys())
    print("RU missing vs EN:", sorted(en_keys - ru_keys))
    print("PL missing vs EN:", sorted(en_keys - pl_keys))
except Exception as e:
    print("JSON Error:", e)
