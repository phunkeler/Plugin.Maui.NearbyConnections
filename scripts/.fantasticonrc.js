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
    'checkmark': 0xe003,
    'discovering': 0xe004,
    'down': 0xe005,
    'link': 0xe006,
    'right': 0xe007,
    'send': 0xe008,
    'settings': 0xe009,
    'users': 0xe010
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