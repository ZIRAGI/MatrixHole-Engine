with open("wwwroot/js/i18n.js", "rb") as f:
    data = f.read()
print("UTF-8 valid:", True)
ru_start = data.find(b'"ru":')
ru_end = data.find(b'"pl":', ru_start)
ru_section = data[ru_start:ru_end]
corrupt = b'\xef\xbf\xbd' in ru_section
print("Corrupt chars in ru:", corrupt)
if corrupt:
    print("Found corrupted chars!")
else:
    print("ru section is clean")
