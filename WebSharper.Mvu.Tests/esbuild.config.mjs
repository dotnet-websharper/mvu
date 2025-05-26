import { existsSync, cpSync, readdirSync } from 'fs'
import { build } from 'esbuild'

const files = readdirSync('./build/');

cpSync('./build/WebSharper.Mvu.Tests.css', './wwwroot/Scripts/WebSharper.Mvu.Tests.css', { force: true });

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
