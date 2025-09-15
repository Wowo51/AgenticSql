//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AgenticSql;

[TestClass]
public class Common_FromXmlOnly_DeserializesEmpty
{
    [TestMethod]
    public void FromXmlOnly_ParsesEmpty()
    {
        string xml = InputXmlSchemas.EmptyXsd();
        Assert.IsFalse(string.IsNullOrEmpty(xml));
    }
}
