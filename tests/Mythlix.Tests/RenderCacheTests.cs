using Mythlix.Core;
using Mythlix.Infrastructure;
using Xunit;
namespace Mythlix.Tests;
public sealed class RenderCacheTests {
 sealed class Renderer : IPdfRenderer {
  public int Images, Texts;
  public Task<byte[]> RenderAsync(string path,int page,int width,CancellationToken token) { Images++; return Task.FromResult(new byte[]{(byte)Images,2,3,4}); }
  public Task<string> ExtractTextAsync(string path,int page,CancellationToken token) { Texts++; return Task.FromResult("text"); }
 }
 [Fact] public async Task ReusesResultsAndProtectsImageBytes() {
  var source=new Renderer();var cache=new CachedPdfRenderer(source);
  var first=await cache.RenderAsync("a",1,1600,default);first[0]=99;
  Assert.Equal(1,(await cache.RenderAsync("a",1,1600,default))[0]);
  await cache.ExtractTextAsync("a",1,default);await cache.ExtractTextAsync("a",1,default);
  Assert.Equal(1,source.Images);Assert.Equal(1,source.Texts);
 }
 [Fact] public async Task LruEvictsLeastRecentlyUsedAcrossDocuments() {
  var source=new Renderer();var cache=new CachedPdfRenderer(source,8);
  await cache.RenderAsync("a",1,1600,default);await cache.RenderAsync("b",1,1600,default);
  await cache.RenderAsync("a",1,1600,default);await cache.RenderAsync("c",1,1600,default);
  await cache.RenderAsync("a",1,1600,default);Assert.Equal(3,source.Images);
  await cache.RenderAsync("b",1,1600,default);Assert.Equal(4,source.Images);Assert.Equal(8,cache.CachedBytes);
 }
 [Fact] public async Task InvalidationRefreshesOnlyAffectedDocumentAndAllSizes() {
  var source=new Renderer();var cache=new CachedPdfRenderer(source);
  await cache.RenderAsync("a",1,1600,default);await cache.RenderAsync("a",1,260,default);
  await cache.ExtractTextAsync("a",1,default);await cache.RenderAsync("b",1,1600,default);
  cache.Invalidate("a");await cache.RenderAsync("b",1,1600,default);Assert.Equal(3,source.Images);
  await cache.RenderAsync("a",1,1600,default);await cache.RenderAsync("a",1,260,default);
  await cache.ExtractTextAsync("a",1,default);Assert.Equal(5,source.Images);Assert.Equal(2,source.Texts);
  cache.Clear();Assert.Equal(0,cache.CachedBytes);
 }
 [Fact] public async Task OversizedResultsAndCancelledRequestsAreNotCached() {
  var source=new Renderer();var cache=new CachedPdfRenderer(source,3);
  await cache.RenderAsync("a",1,1600,default);await cache.RenderAsync("a",1,1600,default);
  Assert.Equal(2,source.Images);Assert.Equal(0,cache.CachedBytes);
  using var cts=new CancellationTokenSource();cts.Cancel();
  await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>cache.RenderAsync("a",1,1600,cts.Token));
  Assert.Equal(2,source.Images);
 }
 [Fact] public async Task InFlightResultCannotRepopulateInvalidatedCache() {
  var pending=new TaskCompletionSource<byte[]>();var source=new PendingRenderer(pending.Task);var cache=new CachedPdfRenderer(source);
  var task=cache.RenderAsync("a",1,1600,default);cache.Invalidate("a");pending.SetResult([1,2]);await task;
  Assert.Equal(0,cache.CachedBytes);
 }
 sealed class PendingRenderer(Task<byte[]> pending) : IPdfRenderer {
  public Task<byte[]> RenderAsync(string path,int page,int width,CancellationToken token)=>pending;
  public Task<string> ExtractTextAsync(string path,int page,CancellationToken token)=>Task.FromResult("");
 }
}
