import re

with open('wwwroot/index.html', 'r', encoding='utf-8') as f:
    html = f.read()
keys = set(re.findall(r'data-i18n="([^"]+)"', html))

with open('wwwroot/js/i18n.js', 'r', encoding='utf-8') as f:
    i18n = f.read()
en_start = i18n.find('"en":')
ru_start = i18n.find('"ru":', en_start)
en = i18n[en_start:ru_start]
en_keys = set(re.findall(r'"([^"]+)":\s*"', en))

missing = keys - en_keys
print('Missing in EN:', missing)

extra = sorted(en_keys - keys)
print('Extra in EN (first 30):', extra[:30])
