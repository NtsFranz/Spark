export const prerender = true;
// export const csr = false;
export const ssr = false;
// Emit every route as <route>/index.html so /overlays/, /components/minimap/ etc.
// resolve through the overlay server's default-file handling (as in the original Spark).
export const trailingSlash = 'always';
