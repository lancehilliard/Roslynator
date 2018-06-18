﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Roslynator.CSharp
{
    //TODO: rename to ExpressionChain
    //TODO: make public
    /// <summary>
    /// Enables to enumerate expressions of binary expression and expressions of nested binary expressions of the same kind.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal partial struct BinaryExpressionChain : IEquatable<BinaryExpressionChain>, IEnumerable<ExpressionSyntax>
    {
        internal BinaryExpressionChain(BinaryExpressionSyntax binaryExpression)
        {
            BinaryExpression = binaryExpression;
            Span = binaryExpression.FullSpan;
        }

        internal BinaryExpressionChain(BinaryExpressionSyntax binaryExpression, TextSpan span)
        {
            BinaryExpression = binaryExpression;
            Span = span;
        }

        /// <summary>
        /// The binary expression.
        /// </summary>
        public BinaryExpressionSyntax BinaryExpression { get; }

        /// <summary>
        /// The text span.
        /// </summary>
        public TextSpan Span { get; }

        internal TextSpan ExpressionsSpan
        {
            get
            {
                Enumerator en = GetEnumerator();

                if (en.MoveNext())
                {
                    int end = en.Current.Span.End;

                    int start = en.Current.SpanStart;

                    while (en.MoveNext())
                        start = en.Current.SpanStart;

                    return TextSpan.FromBounds(start, end);
                }

                return default;
            }
        }

        private int Count
        {
            get
            {
                int count = 0;

                Enumerator en = GetEnumerator();
                while (en.MoveNext())
                    count++;

                return count;
            }
        }

        internal ExpressionSyntax FirstExpression
        {
            get
            {
                Enumerator en = GetEnumerator();

                return (en.MoveNext()) ? en.Current : null;
            }
        }

        internal ExpressionSyntax LastExpression
        {
            get
            {
                Enumerator en = GetEnumerator();

                if (en.MoveNext())
                {
                    ExpressionSyntax e = en.Current;

                    while (en.MoveNext())
                        e = en.Current;

                    return e;
                }

                return default;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get { return (BinaryExpression != null) ? $"Count = {Count} {BinaryExpression}" : "Uninitialized"; }
        }

        public Reversed Reverse()
        {
            return new Reversed(this);
        }

        internal bool ContainsMultiLineExpression()
        {
            Enumerator en = GetEnumerator();

            while (en.MoveNext())
            {
                if (en.Current.IsMultiLine(includeExteriorTrivia: false))
                    return true;
            }

            return false;
        }

        internal bool IsStringConcatenation(
            SemanticModel semanticModel,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!BinaryExpression.IsKind(SyntaxKind.AddExpression))
                return false;

            Enumerator en = GetEnumerator();

            if (!en.MoveNext())
                return false;

            var binaryExpression = (BinaryExpressionSyntax)en.Current.Parent;

            if (!en.MoveNext())
                return false;

            while (true)
            {
                if (!CSharpUtility.IsStringConcatenation(binaryExpression, semanticModel, cancellationToken))
                    return false;

                ExpressionSyntax prev = en.Current;

                if (en.MoveNext())
                {
                    binaryExpression = (BinaryExpressionSyntax)prev.Parent;
                }
                else
                {
                    break;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the enumerator for the expressions.
        /// </summary>
        /// <returns></returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<ExpressionSyntax> IEnumerable<ExpressionSyntax>.GetEnumerator()
        {
            if (BinaryExpression != null)
                return new EnumeratorImpl(this);

            return Empty.Enumerator<ExpressionSyntax>();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            if (BinaryExpression != null)
                return new EnumeratorImpl(this);

            return Empty.Enumerator<ExpressionSyntax>();
        }

        /// <summary>
        /// Returns the string representation of the underlying syntax, not including its leading and trailing trivia.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return BinaryExpression?.ToString() ?? "";
        }

        /// <summary>
        /// Determines whether this instance and a specified object are equal.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance. </param>
        /// <returns>true if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, false. </returns>
        public override bool Equals(object obj)
        {
            return obj is BinaryExpressionChain other && Equals(other);
        }

        /// <summary>
        /// Determines whether this instance is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
        public bool Equals(BinaryExpressionChain other)
        {
            return EqualityComparer<BinaryExpressionSyntax>.Default.Equals(BinaryExpression, other.BinaryExpression);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
        public override int GetHashCode()
        {
            return EqualityComparer<BinaryExpressionSyntax>.Default.GetHashCode(BinaryExpression);
        }

        public static bool operator ==(in BinaryExpressionChain info1, in BinaryExpressionChain info2)
        {
            return info1.Equals(info2);
        }

        public static bool operator !=(in BinaryExpressionChain info1, in BinaryExpressionChain info2)
        {
            return !(info1 == info2);
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "<Pending>")]
        [SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "<Pending>")]
        [SuppressMessage("Usage", "CA2231:Overload operator equals on overriding value type Equals", Justification = "<Pending>")]
        public struct Enumerator
        {
            private BinaryExpressionChain _binaryExpressionChain;
            private ExpressionSyntax _current;
            private State _state;

            internal Enumerator(in BinaryExpressionChain binaryExpressionChain)
            {
                _binaryExpressionChain = binaryExpressionChain;
                _current = null;
                _state = State.Start;
            }

            private BinaryExpressionSyntax BinaryExpression
            {
                get { return _binaryExpressionChain.BinaryExpression; }
            }

            private TextSpan Span
            {
                get { return _binaryExpressionChain.Span; }
            }

            public bool MoveNext()
            {
                switch (_state)
                {
                    case State.Start:
                        {
                            if (BinaryExpression == null)
                                return false;

                            ExpressionSyntax right = BinaryExpression.Right;

                            Debug.Assert(right != null, "BinaryExpressionSyntax.Right is null.");

                            if (IsInSpan(right.Span))
                            {
                                _current = right;
                                _state = State.Right;
                                return true;
                            }

                            BinaryExpressionSyntax binaryExpression = BinaryExpression;

                            while (true)
                            {
                                ExpressionSyntax left = binaryExpression.Left;

                                Debug.Assert(left != null, "BinaryExpressionSyntax.Left is null.");

                                if (left.IsKind(binaryExpression.Kind()))
                                {
                                    binaryExpression = (BinaryExpressionSyntax)left;
                                    right = binaryExpression.Right;

                                    Debug.Assert(right != null, "BinaryExpressionSyntax.Right is null.");

                                    if (IsInSpan(right.Span))
                                    {
                                        _current = right;
                                        _state = State.Right;
                                        return true;
                                    }
                                }
                                else
                                {
                                    _state = State.Left;

                                    if (IsInSpan(left.Span))
                                    {
                                        _current = left;
                                        return true;
                                    }

                                    return false;
                                }
                            }
                        }
                    case State.Right:
                        {
                            var binaryExpression = (BinaryExpressionSyntax)_current.Parent;

                            ExpressionSyntax left = binaryExpression.Left;

                            Debug.Assert(left != null, "BinaryExpressionSyntax.Left is null.");

                            if (left.IsKind(binaryExpression.Kind()))
                            {
                                binaryExpression = (BinaryExpressionSyntax)left;

                                ExpressionSyntax right = binaryExpression.Right;

                                Debug.Assert(right != null, "BinaryExpressionSyntax.Right is null.");

                                if (IsInSpan(right.Span))
                                {
                                    _current = right;
                                    _state = State.Right;
                                    return true;
                                }
                            }

                            _state = State.Left;

                            if (IsInSpan(left.Span))
                            {
                                _current = left;
                                return true;
                            }
                            else
                            {
                                _current = null;
                                return false;
                            }
                        }
                    case State.Left:
                        {
                            return false;
                        }
                    default:
                        {
                            throw new InvalidOperationException();
                        }
                }
            }

            public ExpressionSyntax Current
            {
                get { return _current ?? throw new InvalidOperationException(); }
            }

            public void Reset()
            {
                _current = null;
                _state = State.Start;
            }

            private bool IsInSpan(TextSpan span)
            {
                return Span.OverlapsWith(span)
                    || (span.Length == 0 && Span.IntersectsWith(span));
            }

            public override bool Equals(object obj)
            {
                throw new NotSupportedException();
            }

            public override int GetHashCode()
            {
                throw new NotSupportedException();
            }

            private enum State
            {
                Start = 0,
                Left = 1,
                Right = 2,
            }
        }

        private class EnumeratorImpl : IEnumerator<ExpressionSyntax>
        {
            private Enumerator _en;

            internal EnumeratorImpl(in BinaryExpressionChain binaryExpressionChain)
            {
                _en = new Enumerator(binaryExpressionChain);
            }

            public ExpressionSyntax Current
            {
                get { return _en.Current; }
            }

            object IEnumerator.Current
            {
                get { return _en.Current; }
            }

            public bool MoveNext()
            {
                return _en.MoveNext();
            }

            public void Reset()
            {
                _en.Reset();
            }

            public void Dispose()
            {
            }
        }
    }
}