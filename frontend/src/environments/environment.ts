export const environment = {
  production: false,
  // Caminho relativo: no container o nginx faz proxy de /api para o backend, e
  // em "npm start" o proxy.conf.json do Angular CLI faz o mesmo. Assim a porta
  // escolhida no script de setup não precisa ser recompilada no bundle.
  apiUrl: '/api',
};
