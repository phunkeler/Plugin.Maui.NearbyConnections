export const name = 'NearbyChatIcons';
export const fontHeight = 256;
export const normalize = true;
export const inputDir = '../Resources/FontIcons';
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
    'attachment': 0xe002,
    'chat': 0xe003,
    'check': 0xe004,
    'down': 0xe005,
    'gear': 0xe006,
    'left': 0xe007,
    'link': 0xe008,
    'magnify': 0xe009,
    'message': 0xe010,
    'radio': 0xe011,
    'right': 0xe012,
    'send': 0xe013,
    'sonar': 0xe014,
    'users': 0xe015,
    'wifi': 0xe016,
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