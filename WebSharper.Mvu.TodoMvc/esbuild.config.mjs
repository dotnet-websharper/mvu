import { existsSync, cpSync, readdirSync } from 'fs'
import { build } from 'esbuild'

cpSync('./build/', './wwwroot/Scripts/', { recursive: true });

const files = readdirSync('./build/');

files.forEach(file => {
  if (file.endsWith('.js')) {
    var options =
    {
      entryPoints: ['./build/' + file],
      bundle: true,
      minify: true,
      format: 'iife',
      outfile: 'wwwroot/Scripts/' + file,
      globalName: 'wsbundle'
    };

    console.log("Bundling:", file);
    build(options);
  }
});
