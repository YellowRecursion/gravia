import { defineConfig } from 'orval';

export default defineConfig(() => {
  const input = process.env.GRAVIA_OPENAPI;
  if (!input) {
    throw new Error(
      'Set GRAVIA_OPENAPI to the Host OpenAPI file or URL. No business API exists yet.',
    );
  }

  return {
    gravia: {
      input,
      output: {
        target: './renderer/api/generated/client.ts',
        schemas: './renderer/api/generated/models',
        client: 'react-query',
        httpClient: 'fetch',
        baseUrl: '',
        prettier: true,
      },
    },
  };
});
