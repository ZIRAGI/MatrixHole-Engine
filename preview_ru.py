with open('wwwroot/js/i18n.js', 'r', encoding='utf-8') as f:
    data = f.read()
ru_start = data.find('"ru":')
ru_end = data.find('"pl":', ru_start)
ru = data[ru_start:ru_end]
lines = ru.split('\n')[:20]
for line in lines:
    print(line)
print('...')
