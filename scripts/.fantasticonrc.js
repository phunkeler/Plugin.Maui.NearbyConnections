export const name = 'NearbyChatIcons';
export const fontHeight = 256;
export const normalize = true;
export const inputDir = '../.assets/icons';
export const outputDir = './tmp/NearbyChatIcons';
export const fontTypes = ['ttf'];
export const assetTypes = ['css', 'json', 'html'];
export const formatOptions = {
    json: {
        indent: 2
    }
};
export const codepoints = {
    'antenna': 0xe001,
    'chat': 0xe002,
    'check': 0xe003,
    'down': 0xe004,
    'gear': 0xe005,
    'left': 0xe006,
    'link': 0xe007,
    'magnify': 0xe008,
    'message': 0xe009,
    'radio': 0xe010,
    'right': 0xe011,
    'send': 0xe012,
    'sonar': 0xe013,
    'users': 0xe014,
    'wifi': 0xe015,
};
export function getIconId({
    basename, // `string` - Example: 'foo';
    relativeDirPath, // `string` - Example: 'sub/dir/foo.svg'
    absoluteFilePath, // `string` - Example: '/var/icons/sub/dir/foo.svg'
    relativeFilePath, // `string` - Example: 'foo.svg'
    index // `number` - Example: `0`
}) {
    return basename;
}