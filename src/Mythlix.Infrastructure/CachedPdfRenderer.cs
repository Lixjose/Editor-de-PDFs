using Mythlix.Core;
namespace Mythlix.Infrastructure;

/// <summary>Workspace-wide LRU of encoded images and extracted text. Call Invalidate before replacing a working PDF.</summary>
public sealed class CachedPdfRenderer(IPdfRenderer inner, long capacity = 32L * 1024 * 1024) : IPdfRenderer {
 readonly object gate = new();
 readonly LinkedList<(Key Key, object Value, long Size)> lru = new();
 readonly Dictionary<Key, LinkedListNode<(Key Key, object Value, long Size)>> entries = new();
 long generation;
 long size;
 readonly record struct Key(string Path, int Page, int Width);
 public long CachedBytes { get { lock(gate) return size; } }
 public void Invalidate(string path) {
  lock(gate) {
   generation++;
   foreach(var key in entries.Keys.Where(k => k.Path == path).ToArray()) Remove(entries[key]);
  }
 }
 public void Clear() { lock(gate) { generation++; entries.Clear(); lru.Clear(); size = 0; } }
 void Remove(LinkedListNode<(Key Key, object Value, long Size)> node) {
  entries.Remove(node.Value.Key); lru.Remove(node); size -= node.Value.Size;
 }
 async Task<T> GetAsync<T>(Key key, Func<Task<T>> load, Func<T,long> measure, CancellationToken token) where T : class {
  token.ThrowIfCancellationRequested();
  long version;
  lock(gate) {
   version = generation;
   if(entries.TryGetValue(key, out var node)) { lru.Remove(node); lru.AddFirst(node); return (T)node.Value.Value; }
  }
  var value = await load();
  token.ThrowIfCancellationRequested();
  var bytes = measure(value);
  lock(gate) {
   if(version != generation || bytes > capacity || capacity <= 0) return value;
   if(entries.TryGetValue(key, out var existing)) Remove(existing);
   while(size + bytes > capacity && lru.Last is {} last) Remove(last);
   entries[key] = lru.AddFirst((key, value, bytes)); size += bytes;
  }
  return value;
 }
 public async Task<byte[]> RenderAsync(string path, int page, int width, CancellationToken token) {
  width = Math.Clamp(width, 128, 2400);
  var data = await GetAsync(new(path,page,width), () => inner.RenderAsync(path,page,width,token), b => b.LongLength, token);
  return (byte[])data.Clone();
 }
 public Task<string> ExtractTextAsync(string path, int page, CancellationToken token) =>
  GetAsync(new(path,page,0), () => inner.ExtractTextAsync(path,page,token), s => (long)s.Length * sizeof(char), token);
}
