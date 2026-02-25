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
    'camera': 0xe003,
    'chat': 0xe004,
    'check': 0xe005,
    'down': 0xe006,
    'gear': 0xe007,
    'left': 0xe008,
    'link': 0xe009,
    'magnify': 0xe010,
    'message': 0xe011,
    'radio': 0xe012,
    'right': 0xe013,
    'send': 0xe014,
    'sonar': 0xe015,
    'users': 0xe016,
    'video': 0xe017,
    'wifi': 0xe018,
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