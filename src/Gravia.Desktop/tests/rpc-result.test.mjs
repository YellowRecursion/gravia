import assert from 'node:assert/strict';
import { test } from 'node:test';
import { getRpcFieldErrors, RpcError, unwrapRpcResult } from '../renderer/shared/api/rpc-result.ts';

test('unwrapRpcResult returns successful data', () => {
  const data = { name: 'demo' };
  assert.equal(
    unwrapRpcResult({
      isSuccess: true,
      errorCode: null,
      errorMessage: null,
      traceId: null,
      errorData: null,
      data,
    }),
    data,
  );
});

test('unwrapRpcResult throws a typed error without interpreting the Host message', () => {
  const result = {
    isSuccess: false,
    errorCode: 'validation_failed',
    errorMessage: 'The request is invalid.',
    traceId: 'trace-1',
    errorData: {
      errors: [{ field: 'name', code: 'name_required', message: 'Name is required.' }],
    },
  };

  assert.throws(
    () => unwrapRpcResult(result),
    (error) =>
      error instanceof RpcError &&
      error.code === 'validation_failed' &&
      error.traceId === 'trace-1',
  );
  try {
    unwrapRpcResult(result);
  } catch (error) {
    assert.deepEqual(getRpcFieldErrors(error), result.errorData.errors);
  }
});

test('getRpcFieldErrors rejects malformed error data', () => {
  const error = new RpcError({
    isSuccess: false,
    errorCode: 'validation_failed',
    errorMessage: 'invalid',
    traceId: 'trace-2',
    errorData: { errors: [{ field: 'name' }, null, 'invalid'] },
  });
  assert.deepEqual(getRpcFieldErrors(error), []);
  assert.deepEqual(getRpcFieldErrors(new Error('network')), []);
});
