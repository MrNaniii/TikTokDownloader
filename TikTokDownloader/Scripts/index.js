const sign = require('./xbogus');

const [,, url, userAgent] = process.argv;

if (!url || !userAgent) {
  console.error('Missing arguments');
  process.exit(1);
}

console.log(sign(url.split("?")[1], userAgent));
