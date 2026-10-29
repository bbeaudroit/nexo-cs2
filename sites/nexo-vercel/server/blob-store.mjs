// Private immutable records. No in-memory production fallback.
// SDK contract: https://vercel.com/docs/vercel-blob/using-blob-sdk
export function createBlobStore({
  token = process.env.BLOB_READ_WRITE_TOKEN,
  storeId = process.env.BLOB_STORE_ID,
  sdk,
} = {}) {
  if (!token && !storeId) return null;
  const client = async () => sdk || (await import("@vercel/blob"));
  // The SDK obtains and refreshes the Vercel OIDC token automatically.
  const auth = token ? { token } : { storeId };
  async function read(key) {
    const api = await client();
    const result = await api.get(key, {
      ...auth,
      access: "private",
      useCache: false,
      // Preserve the strong storage ETag used by conditional catalog writes.
      headers: { "Accept-Encoding": "identity" },
    });
    if (!result) return null;
    if (result.statusCode !== 200 || !result.stream)
      throw Error("Blob read failed");
    return {
      value: JSON.parse(await new Response(result.stream).text()),
      etag: result.blob?.etag,
    };
  }
  async function get(key) {
    return (await read(key))?.value ?? null;
  }
  return {
    get,
    async catalog() {
      const shards = Array.from(
        "0123456789abcdef",
        (hex) => `catalog/${hex}.json`,
      );
      const entries = [];
      // Four concurrent reads bound network pressure; only metadata is downloaded.
      for (let offset = 0; offset < shards.length; offset += 4) {
        const group = await Promise.all(
          shards.slice(offset, offset + 4).map(get),
        );
        for (const shard of group)
          if (shard) entries.push(...Object.values(shard));
      }
      return entries;
    },
    async index(cfg) {
      if (!/^[a-f0-9]{64}$/.test(cfg.id))
        throw Error("Invalid catalog identity");
      const key = `catalog/${cfg.id[0]}.json`,
        api = await client();
      const { code, ...metadata } = cfg;
      for (let attempt = 0; attempt < 8; attempt++) {
        const previous = await read(key),
          shard = previous?.value || {};
        if (!shard[cfg.id] && Object.keys(shard).length >= 1000)
          throw Object.assign(Error("CFG catalog shard is full."), {
            status: 503,
          });
        const current = shard[cfg.id];
        const next = {
          ...(current || metadata),
          users: Math.max(current?.users || 0, metadata.users || 0),
        };
        if (current && JSON.stringify(current) === JSON.stringify(next))
          return current;
        shard[cfg.id] = next;
        if (previous && !previous.etag) throw Error("Catalog ETag unavailable");
        try {
          await api.put(key, JSON.stringify(shard), {
            ...auth,
            access: "private",
            contentType: "application/json",
            addRandomSuffix: false,
            allowOverwrite: !!previous,
            ...(previous ? { ifMatch: previous.etag } : {}),
          });
          return next;
        } catch (error) {
          if (
            api.BlobPreconditionFailedError &&
            error instanceof api.BlobPreconditionFailedError
          )
            continue;
          const latest = await read(key);
          if (latest?.etag && latest.etag !== previous?.etag) continue;
          throw error;
        }
      }
      throw Error("Catalog write conflict. Retry shortly.");
    },
    async putIfAbsent(key, value) {
      const api = await client();
      try {
        await api.put(key, JSON.stringify(value), {
          ...auth,
          access: "private",
          contentType: "application/json",
          addRandomSuffix: false,
          allowOverwrite: false,
        });
        return true;
      } catch (error) {
        if (
          api.BlobAlreadyExistsError &&
          error instanceof api.BlobAlreadyExistsError
        )
          return false;
        // Current SDK versions can report a duplicate as a generic BlobError.
        // Confirm the durable record exists instead of parsing error messages.
        if (await get(key)) return false;
        throw error;
      }
    },
    async list(prefix) {
      const api = await client();
      const result = [];
      let cursor;
      do {
        const page = await api.list({
          ...auth,
          prefix,
          limit: 1000,
          ...(cursor ? { cursor } : {}),
        });
        for (const blob of page.blobs) {
          result.push({
            key: blob.pathname,
            ...(prefix.startsWith("cfgs/")
              ? { value: await get(blob.pathname) }
              : {}),
          });
        }
        if (page.hasMore && !page.cursor)
          throw Error("Incomplete Blob pagination");
        cursor = page.hasMore ? page.cursor : undefined;
      } while (cursor);
      return result;
    },
  };
}
