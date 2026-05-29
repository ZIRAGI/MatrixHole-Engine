with open('wwwroot/index.html', 'r', encoding='utf-8') as f:
    data = f.read()

# Find and replace the corrupted Russian button
old = '<button class="lang-btn" data-lang="ru">'
start = data.find(old)
if start != -1:
    end_tag = data.find('</button>', start)
    current = data[start:end_tag+9]
    print('Current:', repr(current))
    new = '<button class="lang-btn" data-lang="ru">Русский</button>'
    data = data.replace(current, new)
    with open('wwwroot/index.html', 'w', encoding='utf-8') as f:
        f.write(data)
    print('Fixed!')
else:
    print('Pattern not found')
