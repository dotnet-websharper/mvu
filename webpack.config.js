const path = require('path');

module.exports = {
  entry: './WebSharper.Mvu/src/index.js',
  output: {
    filename: 'remotedev.js',
    path: path.resolve(__dirname, 'WebSharper.Mvu', 'dist')
  },
  devServer: {
    contentBase: path.join(__dirname, 'WebSharper.Mvu.TodoMvc', 'wwwroot')
  }
};
