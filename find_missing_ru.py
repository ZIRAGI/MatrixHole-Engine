import json, re

with open('wwwroot/js/i18n.js', 'r', encoding='utf-8') as f:
    data = f.read()

def extract_section(name):
    start = data.find(f'"{name}":')
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
    return json.loads(data[brace_start:end+1])

en = extract_section('en')
ru = extract_section('ru')

missing = sorted(set(en.keys()) - set(ru.keys()))
print('Missing RU keys:', missing)
for k in missing:
    print(f'  {k}: {en[k]}')
