with open('wwwroot/js/i18n.js', 'r', encoding='utf-8') as f:
    data = f.read()
en_start = data.find('"en":')
ru_start = data.find('"ru":', en_start)
en = data[en_start:ru_start]
for key in ['account', 'link_steam', 'unlink_steam', 'steam_link_desc', 'discord_auth_desc']:
    if f'"{key}":' in en:
        print(f'{key}: found')
    else:
        print(f'{key}: MISSING')
