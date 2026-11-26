using HerProof.Domain.Assessments;

namespace HerProof.Application.Assessments;

/// <summary>Default practical questions for a C# assessment (docs/05 P13 polish): one "implement a
/// feature" question and one "find and fix the bug" question, each with real, realistic starter
/// code — not a single blank textarea with no prompt. Applied once, at assessment creation time;
/// an assessor can still edit or add their own questions afterward (not built here, out of scope
/// for the hackathon demo).</summary>
public static class CSharpAssessmentQuestionTemplate
{
    public static void ApplyTo(Assessment assessment)
    {
        assessment.AddQuestion(
            1,
            "Implement: Shopping cart total",
            "Complete the `CalculateTotal` method on `ShoppingCart` so it returns the sum of each " +
            "item's Price times Quantity, with a 10% discount applied when the total exceeds 500.",
            """
            public class ShoppingCart
            {
                public List<CartItem> Items { get; set; } = new();

                public decimal CalculateTotal()
                {
                    // TODO: implement this method.
                    throw new NotImplementedException();
                }
            }

            public class CartItem
            {
                public string Name { get; set; } = string.Empty;
                public decimal Price { get; set; }
                public int Quantity { get; set; }
            }
            """,
            "ShoppingCart.cs",
            AssessmentQuestionType.Implement);

        assessment.AddQuestion(
            2,
            "Fix the bug: pagination off-by-one",
            "`GetPage` is supposed to return items 1-10 for page 1, 11-20 for page 2, and so on. " +
            "It has a bug — find it and fix it.",
            """
            public class Paginator
            {
                public List<string> GetPage(List<string> items, int pageNumber, int pageSize)
                {
                    var skip = pageNumber * pageSize;
                    return items.Skip(skip).Take(pageSize).ToList();
                }
            }
            """,
            "Paginator.cs",
            AssessmentQuestionType.FixBug);
    }
}
