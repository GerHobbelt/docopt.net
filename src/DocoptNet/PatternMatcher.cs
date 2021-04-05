namespace DocoptNet
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    static class PatternMatcher
    {
        public static MatchResult Match(this Pattern pattern, IList<LeafPattern> left, IEnumerable<LeafPattern> collected)
        {
            var coll = collected ?? new List<LeafPattern>();

            switch (pattern)
            {
                case Required required:
                {
                    var l = left;
                    var c = coll;
                    foreach (var child in required.Children)
                    {
                        bool matched;
                        (matched, l, c) = child.Match(l, c);
                        if (!matched)
                            return new MatchResult(false, left, coll);
                    }
                    return new MatchResult(true, l, c);
                }
                case Either either:
                {
                    var outcomes =
                        either.Children.Select(pattern => Match(pattern, left, coll))
                              .Where(outcome => outcome.Matched)
                              .ToList();
                    if (outcomes.Count != 0)
                    {
                        var minCount = outcomes.Min(x => x.Left.Count);
                        return outcomes.First(x => x.Left.Count == minCount);
                    }
                    return new MatchResult(false, left, coll);
                }
                case Optional optional:
                {
                    var l = left;
                    var c = coll;
                    foreach (var child in optional.Children)
                        (_, l, c) = child.Match(l, c);
                    return new MatchResult(true, l, c);
                }
                case OneOrMore oneOrMore:
                {
                    Debug.Assert(oneOrMore.Children.Count == 1);
                    var l = left;
                    var c = coll;
                    IList<LeafPattern> l_ = null;
                    var matched = true;
                    var times = 0;
                    while (matched)
                    {
                        // could it be that something didn't match but changed l or c?
                        (matched, l, c) = oneOrMore.Children[0].Match(l, c);
                        times += matched ? 1 : 0;
                        if (l_ != null && l_.Equals(l))
                            break;
                        l_ = l;
                    }
                    if (times >= 1)
                    {
                        return new MatchResult(true, l, c);
                    }
                    return new MatchResult(false, left, coll);
                }
                case LeafPattern leaf:
                {
                    var (index, match) = SingleMatch(leaf, left);
                    if (match == null)
                    {
                        return new MatchResult(false, left, coll);
                    }
                    var left_ = new List<LeafPattern>();
                    left_.AddRange(left.Take(index));
                    left_.AddRange(left.Skip(index + 1));
                    var sameName = coll.Where(a => a.Name == leaf.Name).ToList();
                    if (leaf.Value != null && (leaf.Value.IsList || leaf.Value.IsOfTypeInt))
                    {
                        var increment = new ValueObject(1);
                        if (!leaf.Value.IsOfTypeInt)
                        {
                            increment = match.Value.IsString ? new ValueObject(new [] {match.Value})  : match.Value;
                        }
                        if (sameName.Count == 0)
                        {
                            match.Value = increment;
                            var res = new List<LeafPattern>(coll) {match};
                            return new MatchResult(true, left_, res);
                        }
                        sameName[0].Value.Add(increment);
                        return new MatchResult(true, left_, coll);
                    }
                    var resColl = new List<LeafPattern>();
                    resColl.AddRange(coll);
                    resColl.Add(match);
                    return new MatchResult(true, left_, resColl);
                }
                default: throw new ArgumentException(nameof(pattern));
            }

            static (int, LeafPattern) SingleMatch(LeafPattern pattern, IList<LeafPattern> left)
            {
                switch (pattern)
                {
                    case Command command:
                    {
                        for (var i = 0; i < left.Count; i++)
                        {
                            if (left[i] is Argument { Value: { } value })
                            {
                                if (value.ToString() == command.Name)
                                    return (i, new Command(command.Name, new ValueObject(true)));
                                break;
                            }
                        }
                        return default;
                    }
                    case Argument argument:
                    {
                        for (var i = 0; i < left.Count; i++)
                        {
                            if (left[i] is Argument { Value: var value })
                                return (i, new Argument(argument.Name, value));
                        }
                        return default;
                    }
                    case Option option:
                    {
                        for (var i = 0; i < left.Count; i++)
                        {
                            if (left[i].Name == option.Name)
                                return (i, left[i]);
                        }
                        return default;
                    }
                    default: throw new ArgumentException(nameof(pattern));
                }
            }
        }
    }
}