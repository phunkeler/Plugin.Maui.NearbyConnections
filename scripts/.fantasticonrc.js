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
    'settings': 0xe001,
    'send': 0xe002,
    'advertising': 0xe003,
    'connected': 0xe004,
    'disconnected': 0xe005,
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