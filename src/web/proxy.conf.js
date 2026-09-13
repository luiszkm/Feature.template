/**
 * The dev server proxies `/api` so the browser never makes a cross-origin call - which is why
 * `Cors:AllowedOrigins` in the API needs no entry for the front.
 */
module.exports = {
  '/api': {
    target: process.env['API_URL'] || 'http://localhost:5080',
    secure: false,
    changeOrigin: true,
  },
};
