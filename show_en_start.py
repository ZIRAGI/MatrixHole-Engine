with open('wwwroot/js/i18n.js', 'r', encoding='utf-8') as f:
    data = f.read()
en_start = data.find('"en":')
print(data[en_start:en_start+800])
