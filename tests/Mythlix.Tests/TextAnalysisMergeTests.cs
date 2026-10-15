using Mythlix.Core;
using Mythlix.Infrastructure.TextEditing;
using Xunit;

namespace Mythlix.Tests;

public class TextAnalysisMergeTests {
 static OriginalTextBlock Row(string id, double y, string text="Fila") =>
  new(id,text,"Helvetica",12,new(40,y,200,16),id);
 [Fact] public void EditedAndUneditedRowsRemainAvailableWithoutDuplicates() {
  var edited=Row("0:0",40,"Editada");
  var original=Row("v:1",80);
  var merged=VectorTextExtractor.Merge(new([edited],1,""),new([Row("v:0",41,"Editada"),original],0,""));
  Assert.Equal(2,merged.Blocks.Count);
  Assert.Same(edited,merged.Blocks[0]);
  Assert.Same(original,merged.Blocks[1]);
 }
 [Fact] public void AdjacentRowsAreNotRemovedByMerge() {
  var merged=VectorTextExtractor.Merge(new([Row("0:0",40)],0,""),new([Row("v:1",56)],0,""));
  Assert.Equal(2,merged.Blocks.Count);
 }
}
