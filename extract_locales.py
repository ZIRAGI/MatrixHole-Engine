import json, re

with open('wwwroot/js/i18n.js', 'r', encoding='utf-8') as f:
    data = f.read()

def extract_section(name):
    start = data.find(f'"{name}":')
    if start == -1:
        return {}
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
    return json.loads(obj_str)

en = extract_section('en')
pl = extract_section('pl')

# Save as Python dict literals for inspection
with open('en_dict.py', 'w', encoding='utf-8') as f:
    f.write('EN = {\n')
    for k, v in en.items():
        v = v.replace('"', '\\"').replace('\n', '\\n')
        f.write(f'    "{k}": "{v}",\n')
    f.write('}\n')

with open('pl_dict.py', 'w', encoding='utf-8') as f:
    f.write('PL = {\n')
    for k, v in pl.items():
        v = v.replace('"', '\\"').replace('\n', '\\n')
        f.write(f'    "{k}": "{v}",\n')
    f.write('}\n')

print(f'EN: {len(en)} keys')
print(f'PL: {len(pl)} keys')
