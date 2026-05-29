import re
with open('wwwroot/js/i18n.js', 'r', encoding='utf-8') as f:
    data = f.read()
ru_start = data.find('"ru":')
ru_end = data.find('"pl":', ru_start)
ru = data[ru_start:ru_end]
# Find tab_graphics line
m = re.search(r'"tab_graphics":\s*"([^"]+)"', ru)
if m:
    print("tab_graphics ru value:", repr(m.group(1)))
    # Show bytes
    print("as utf-8 bytes:", m.group(1).encode('utf-8'))
else:
    print("not found")
