with open('wwwroot/js/i18n.js', 'r', encoding='utf-8') as f:
    data = f.read()
ru_start = data.find('"ru":')
print('ru header:', repr(data[ru_start:ru_start+60]))
pl_start = data.find('"pl":')
print('pl header:', repr(data[pl_start:pl_start+60]))
