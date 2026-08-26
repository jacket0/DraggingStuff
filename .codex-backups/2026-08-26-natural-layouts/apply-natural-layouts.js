const fs = require('fs');

const itemReferences = [
  '{fileID: 1687898032491626668, guid: 761b8bc554e31394081c8eea4fa65122, type: 3}',
  '{fileID: 485221916132092154, guid: 7ec95db4cb4b7504a8ec40a8846454c2, type: 3}',
  '{fileID: 8278389911820959414, guid: 6d1ba0d7405338145b35ef6f62024f05, type: 3}',
  '{fileID: 1919988485504724112, guid: f9bb4f1ab4314344a97b1f4598ebe396, type: 3}'
];

const results = JSON.parse(fs.readFileSync('Temp/natural-layouts.json', 'utf8'));

for (const result of results) {
  const assetPath = `Assets/Levels/Level_0${result.levelNumber}.asset`;
  const original = fs.readFileSync(assetPath, 'utf8');
  const newline = original.includes('\r\n') ? '\r\n' : '\n';
  const shelvesMarker = `  _shelves:${newline}`;
  const markerIndex = original.indexOf(shelvesMarker);

  if (markerIndex < 0)
    throw new Error(`Shelves marker is missing in ${assetPath}`);

  const outputLines = [];

  for (const shelf of result.layout) {
    outputLines.push('  - _layers:');

    for (const layer of shelf) {
      outputLines.push('    - _itemPrefabs:');

      for (const itemType of layer)
        outputLines.push(`      - ${itemType === null ? '{fileID: 0}' : itemReferences[itemType]}`);
    }
  }

  const prefix = original.slice(0, markerIndex + shelvesMarker.length);
  fs.writeFileSync(assetPath, prefix + outputLines.join(newline) + newline, 'utf8');
}

process.stdout.write('Natural layouts applied\n');
