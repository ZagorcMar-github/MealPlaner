namespace MealPlaner.Models
{
    public class SpecialCases
    {
        public Dictionary<string, List<string>> SpecialCasePatterns { get; private set; } = new Dictionary<string, List<string>>
        { 
        {"soy milk", new List<string> {@"\bsoy\s*milk(s|es)?\b(?![a-zA-Z-])"}},
        {"soymilk", new List<string> {@"\bsoy\s*milk(s|es)?\b(?![a-zA-Z-])"}},
        {"soybean", new List<string> {@"\bsoy\s*bean(s|es)?\b(?![a-zA-Z-])"}},
        {"soy bean", new List<string> {@"\bsoy\s*bean(s|es)?\b(?![a-zA-Z-])"}},
        {"soy", new List<string> {@"\b(soymilk|soy|soybean|bean\s*sprouts)(s|es)?\b(?![a-zA-Z-])"}},
        {"cilantro", new List<string> {@"\b(cilantro|coriander)(s|es)?\b(?![a-zA-Z-])"}},
        {"coriander", new List<string> {@"\b(cilantro|coriander)(s|es)?\b(?![a-zA-Z-])"}},
        {"pasta", new List<string> {@"\b(spaghetti|spaghettini|linguine|pasta|pappardelle|rigatoni|rigaton|penne|fusilli|fettuccine|tagliatelle)(s|es)?\b(?![a-zA-Z-])"}},
        {"linguine", new List<string> {@"\b(spaghetti|spaghettini|linguine|pasta|pappardelle|rigatoni|rigaton|penne|fusilli|fettuccine|tagliatelle)(s|es)?\b(?![a-zA-Z-])"}},
        {"spaghetti", new List<string> {@"\b(spaghetti|spaghettini|linguine|pasta|pappardelle|rigatoni|rigaton|penne|fusilli|fettuccine|tagliatelle)(s|es)?\b(?![a-zA-Z-])"}},
        {"spaghettini", new List<string> {@"\b(spaghetti|spaghettini|linguine|pasta|pappardelle|rigatoni|rigaton|penne|fusilli|fettuccine|tagliatelle)(s|es)?\b(?![a-zA-Z-])"}},
        {"pappardelle", new List<string> {@"\b(spaghetti|spaghettini|linguine|pasta|pappardelle|rigatoni|rigaton|penne|fusilli|fettuccine|tagliatelle)(s|es)?\b(?![a-zA-Z-])"}},
        {"rigatoni", new List<string> {@"\b(spaghetti|spaghettini|linguine|pasta|pappardelle|rigatoni|rigaton|penne|fusilli|fettuccine|tagliatelle)(s|es)?\b(?![a-zA-Z-])"}},
        {"rigaton", new List<string> {@"\b(spaghetti|spaghettini|linguine|pasta|pappardelle|rigatoni|rigaton|penne|fusilli|fettuccine|tagliatelle)(s|es)?\b(?![a-zA-Z-])"}},
        {"penne", new List<string> {@"\b(spaghetti|spaghettini|linguine|pasta|pappardelle|rigatoni|rigaton|penne|fusilli|fettuccine|tagliatelle)(s|es)?\b(?![a-zA-Z-])"}},
        {"fusilli", new List<string> {@"\b(spaghetti|spaghettini|linguine|pasta|pappardelle|rigatoni|rigaton|penne|fusilli|fettuccine|tagliatelle)(s|es)?\b(?![a-zA-Z-])"}},
        {"fettuccine", new List<string> {@"\b(spaghetti|spaghettini|linguine|pasta|pappardelle|rigatoni|rigaton|penne|fusilli|fettuccine|tagliatelle)(s|es)?\b(?![a-zA-Z-])"}},
        {"tagliatelle", new List<string> {@"\b(spaghetti|spaghettini|linguine|pasta|pappardelle|rigatoni|rigaton|penne|fusilli|fettuccine|tagliatelle)(s|es)?\b(?![a-zA-Z-])"}},
        {"Linguini", new List<string> {@"\b(spaghetti|spaghettini|linguine|pasta|pappardelle|rigatoni|rigaton|penne|fusilli|fettuccine|tagliatelle)(s|es)?\b(?![a-zA-Z-])"}},
        {"cannelloni", new List<string> {@"\b(spaghetti|spaghettini|linguine|pasta|pappardelle|rigatoni|rigaton|penne|fusilli|fettuccine|tagliatelle|cannelloni)(s|es)?\b(?![a-zA-Z-])"}}
    };
    }
}
