﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NCalcAsync.Domain
{
    public partial class EvaluationVisitor : LogicalExpressionVisitor
    {
        private delegate T Func<T>();


        private readonly EvaluateOptions _options = EvaluateOptions.None;
        private readonly EvaluateParameterAsyncHandler _evaluateParameterAsync;
        private readonly EvaluateFunctionAsyncHandler _evaluateFunctionAsync;

        private bool IgnoreCase { get { return (_options & EvaluateOptions.IgnoreCase) == EvaluateOptions.IgnoreCase; } }

        public EvaluationVisitor(EvaluateOptions options, ushort? time, uint? step, float? deltaTime, EvaluateParameterAsyncHandler evaluateParameterAsync, EvaluateFunctionAsyncHandler evaluateFunctionAsync)
        {
            _options = options;
            _time = time;
            _step = step;
            _deltaTime = deltaTime;
            _evaluateParameterAsync = evaluateParameterAsync;
            _evaluateFunctionAsync = evaluateFunctionAsync;
        }

        public object Result { get; private set; }

        private async Task<object> EvaluateAsync(LogicalExpression expression)
        {
            await expression.AcceptAsync(this);
            return Result;
        }

        public override Task VisitAsync(LogicalExpression expression)
        {
            return Task.FromException(new Exception("The method or operation is not implemented."));
        }

        private static Type[] CommonTypes = new[] { typeof(Int64), typeof(Double), typeof(Boolean), typeof(String), typeof(Decimal) };

        /// <summary>
        /// Gets the the most precise type.
        /// </summary>
        /// <param name="a">Type a.</param>
        /// <param name="b">Type b.</param>
        /// <returns></returns>
        private static Type GetMostPreciseType(Type a, Type b)
        {
            foreach (Type t in CommonTypes)
            {
                if (a == t || b == t)
                {
                    return t;
                }
            }

            return a;
        }

        public int CompareUsingMostPreciseType(object a, object b)
        {
            Type mpt;
            if (a == null)
            {
                if (b == null)
                    return 0;
                mpt = GetMostPreciseType(null, b.GetType());
            }
            else
            {
                mpt = GetMostPreciseType(a.GetType(), b?.GetType());
            }
            
            return Comparer.Default.Compare(Convert.ChangeType(a, mpt), Convert.ChangeType(b, mpt));
        }

        public override async Task VisitAsync(TernaryExpression expression)
        {
            // Evaluates the left expression and saves the value
            await expression.LeftExpression.AcceptAsync(this);
            bool left = Convert.ToBoolean(Result);

            if (left)
            {
                await expression.MiddleExpression.AcceptAsync(this);
            }
            else
            {
                await expression.RightExpression.AcceptAsync(this);
            }
        }

        private static bool IsReal(object value)
        {
            var typeCode = Type.GetTypeCode(value.GetType());

            return typeCode == TypeCode.Decimal || typeCode == TypeCode.Double || typeCode == TypeCode.Single;
        }

        public override async Task VisitAsync(BinaryExpression expression)
        {
            // simulate Lazy<Func<>> behavior for late evaluation
            object leftValue = null;
            async Task<object> Left()
            {
                if (leftValue == null)
                {
                    await expression.LeftExpression.AcceptAsync(this);
                    leftValue = Result;
                }

                return leftValue;
            }

            // simulate Lazy<Func<>> behavior for late evaluation
            object rightValue = null;
            async Task<object> Right()
            {
                if (rightValue == null)
                {
                    await expression.RightExpression.AcceptAsync(this);
                    rightValue = Result;
                }

                return rightValue;
            }

            switch (expression.Type)
            {
                case BinaryExpressionType.And:
                    Result = Convert.ToBoolean(await Left()) && Convert.ToBoolean(await Right());
                    break;

                case BinaryExpressionType.Or:
                    Result = Convert.ToBoolean(await Left()) || Convert.ToBoolean(await Right());
                    break;

                case BinaryExpressionType.Div:
                    Result = IsReal(await Left()) || IsReal(await Right())
                                 ? Numbers.Divide(await Left(), await Right())
                                 : Numbers.Divide(Convert.ToDouble(await Left()), await Right());
                    break;

                case BinaryExpressionType.Equal:
                    // Use the type of the left operand to make the comparison
                    Result = CompareUsingMostPreciseType(await Left(), await Right()) == 0;
                    break;

                case BinaryExpressionType.Greater:
                    // Use the type of the left operand to make the comparison
                    Result = CompareUsingMostPreciseType(await Left(), await Right()) > 0;
                    break;

                case BinaryExpressionType.GreaterOrEqual:
                    // Use the type of the left operand to make the comparison
                    Result = CompareUsingMostPreciseType(await Left(), await Right()) >= 0;
                    break;

                case BinaryExpressionType.Lesser:
                    // Use the type of the left operand to make the comparison
                    Result = CompareUsingMostPreciseType(await Left(), await Right()) < 0;
                    break;

                case BinaryExpressionType.LesserOrEqual:
                    // Use the type of the left operand to make the comparison
                    Result = CompareUsingMostPreciseType(await Left(), await Right()) <= 0;
                    break;

                case BinaryExpressionType.Minus:
                    Result = Numbers.Soustract(await Left(), await Right());
                    break;

                case BinaryExpressionType.Modulo:
                    Result = Numbers.Modulo(await Left(), await Right());
                    break;

                case BinaryExpressionType.NotEqual:
                    // Use the type of the left operand to make the comparison
                    Result = CompareUsingMostPreciseType(await Left(), await Right()) != 0;
                    break;

                case BinaryExpressionType.Plus:
                    if (await Left() is string)
                    {
                        Result = String.Concat(await Left(), await Right());
                    }
                    else
                    {
                        Result = Numbers.Add(await Left(), await Right());
                    }

                    break;

                case BinaryExpressionType.Times:
                    Result = Numbers.Multiply(await Left(), await Right());
                    break;

                case BinaryExpressionType.BitwiseAnd:
                    Result = Convert.ToUInt16(await Left()) & Convert.ToUInt16(await Right());
                    break;

                case BinaryExpressionType.BitwiseOr:
                    Result = Convert.ToUInt16(await Left()) | Convert.ToUInt16(await Right());
                    break;

                case BinaryExpressionType.BitwiseXOr:
                    Result = Convert.ToUInt16(await Left()) ^ Convert.ToUInt16(await Right());
                    break;

                case BinaryExpressionType.LeftShift:
                    Result = Convert.ToUInt16(await Left()) << Convert.ToUInt16(await Right());
                    break;

                case BinaryExpressionType.RightShift:
                    Result = Convert.ToUInt16(await Left()) >> Convert.ToUInt16(await Right());
                    break;
            }
        }

        public override async Task VisitAsync(UnaryExpression expression)
        {
            // Recursively evaluates the underlying expression
            await expression.Expression.AcceptAsync(this);

            switch (expression.Type)
            {
                case UnaryExpressionType.Not:
                    Result = !Convert.ToBoolean(Result);
                    break;

                case UnaryExpressionType.Negate:
                    Result = Numbers.Soustract(0, Result);
                    break;

                case UnaryExpressionType.BitwiseNot:
                    Result = ~Convert.ToUInt16(Result);
                    break;
            }
        }

        public override Task VisitAsync(ValueExpression expression)
        {
            Result = expression.Value;
            return Task.CompletedTask;
        }

        public override async Task VisitAsync(Function function)
        {
            var args = new FunctionArgs
                           {
                               Parameters = new Expression[function.Expressions.Length]
                           };

            // Don't call parameters right now, instead let the function do it as needed.
            // Some parameters shouldn't be called, for instance, in a if(), the "not" value might be a division by zero
            // Evaluating every value could produce unexpected behaviour
            for (int i = 0; i < function.Expressions.Length; i++)
            {
                args.Parameters[i] = new Expression(function.Expressions[i], _options)
                {
                    // Assign the parameters of the Expression to the arguments so that custom Functions and Parameters can use them
                    Parameters = Parameters,

                    // Pass on the parameter and function evaluators, if any
                    EvaluateParameterAsync = _evaluateParameterAsync,
                    EvaluateFunctionAsync = _evaluateFunctionAsync
                };

            }

            if (_evaluateFunctionAsync != null)
            {
                var name = IgnoreCase ? function.Identifier.Name.ToLower() : function.Identifier.Name;

                // Calls external implementations, which  may be a MulticastDelegate which 
                // requires manual handling for async delegates.
                foreach (var handler in _evaluateFunctionAsync.GetInvocationList().Cast<EvaluateFunctionAsyncHandler>())
                {
                    await handler.Invoke(name, args);
                }
            }

            // If an external implementation was found get the result back
            if (args.HasResult)
            {
                Result = args.Result;
                return;
            }

            switch (function.Identifier.Name.ToLower())
            {
                #region Specifics
                case "value":
                    await VisitValue(function);
                    break;
                case "init":
                    await VisitInit(function);
                    break;
                case "externalupdate":
                    await VisitExternalUpdate(function);
                    break;
                case "ramp":
                    await VisitRamp(function);
                    break;
                case "step":
                    await VisitStep(function);
                    break;
                case "pulse":
                    await VisitPulse(function);
                    break;
                case "normal":
                    await VisitNormal(function);
                    break;
                case "smth1":
                    await VisitSmth1(function);
                    break;
                case "smth3":
                    await VisitSmth3(function);
                    break;
                case "smthn":
                    await VisitSmthN(function);
                    break;
                #endregion

                #region Abs
                case "abs":

                    CheckCase("Abs", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Abs() takes exactly 1 argument");

                    Result = Math.Abs(Convert.ToDecimal(
                        await EvaluateAsync(function.Expressions[0]))
                        );

                    break;

                #endregion

                #region Arccos
                case "arccos":

                    CheckCase("Arccos", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Arccos() takes exactly 1 argument");

                    Result = Math.Acos(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])));

                    break;

                #endregion

                #region Arcsin
                case "arcsin":

                    CheckCase("Arcsin", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Arcsin() takes exactly 1 argument");

                    Result = Math.Asin(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])));

                    break;

                #endregion

                #region Arctan
                case "arctan":

                    CheckCase("Arctan", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Arctan() takes exactly 1 argument");

                    Result = Math.Atan(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])));

                    break;

                #endregion

                #region Ceiling
                case "ceiling":

                    CheckCase("Ceiling", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Ceiling() takes exactly 1 argument");

                    Result = Math.Ceiling(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])));

                    break;

                #endregion

                #region Cos

                case "cos":

                    CheckCase("Cos", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Cos() takes exactly 1 argument");

                    Result = Math.Cos(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])));

                    break;

                #endregion

                #region Exp
                case "exp":

                    CheckCase("Exp", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Exp() takes exactly 1 argument");

                    Result = Math.Exp(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])));

                    break;

                #endregion

                #region Floor
                case "floor":

                    CheckCase("Floor", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Floor() takes exactly 1 argument");

                    Result = Math.Floor(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])));

                    break;

                #endregion

                #region IEEERemainder
                case "ieeeremainder":

                    CheckCase("IEEERemainder", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("IEEERemainder() takes exactly 2 arguments");

                    Result = Math.IEEERemainder(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])), Convert.ToDouble(await EvaluateAsync(function.Expressions[1])));

                    break;

                #endregion

                #region Ln
                case "ln":

                    CheckCase("Ln", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Ln() takes exactly 1 argument");

                    Result = Math.Log(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])));

                    break;

                #endregion

                #region Log
                case "log":

                    CheckCase("Log", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("Log() takes exactly 2 arguments");

                    Result = Math.Log(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])), Convert.ToDouble(await EvaluateAsync(function.Expressions[1])));

                    break;

                #endregion

                #region Log10
                case "log10":

                    CheckCase("Log10", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Log10() takes exactly 1 argument");

                    Result = Math.Log10(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])));

                    break;

                #endregion

                #region Pow
                case "pow":

                    CheckCase("Pow", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("Pow() takes exactly 2 arguments");

                    Result = Math.Pow(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])), Convert.ToDouble(await EvaluateAsync(function.Expressions[1])));

                    break;

                #endregion

                #region Round
                case "round":

                    CheckCase("Round", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("Round() takes exactly 2 arguments");

                    MidpointRounding rounding = (_options & EvaluateOptions.RoundAwayFromZero) == EvaluateOptions.RoundAwayFromZero ? MidpointRounding.AwayFromZero : MidpointRounding.ToEven;

                    Result = Math.Round(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])), Convert.ToInt16(await EvaluateAsync(function.Expressions[1])), rounding);

                    break;

                #endregion

                #region Safediv
                case "safediv":

                    CheckCase("Safediv", function.Identifier.Name);

                    if (function.Expressions.Length > 3 && function.Expressions.Length < 2)
                        throw new ArgumentException("Sign() takes exactly 3 arguments");

                    if (function.Expressions.Length == 3)
                    {
                        if (Convert.ToDouble(await EvaluateAsync(function.Expressions[1])) == 0)
                        {
                            Result = Convert.ToDouble(await EvaluateAsync(function.Expressions[0])) / Convert.ToDouble(await EvaluateAsync(function.Expressions[2]));
                        } else
                        {
                            Result = Convert.ToDouble(await EvaluateAsync(function.Expressions[0])) / Convert.ToDouble(await EvaluateAsync(function.Expressions[1]));
                        }
                    } else { Result = 0; }
                    break;
                #endregion

                #region Sign
                case "sign":

                    CheckCase("Sign", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Sign() takes exactly 1 argument");

                    Result = Math.Sign(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])));

                    break;

                #endregion

                #region Sin
                case "sin":

                    CheckCase("Sin", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Sin() takes exactly 1 argument");

                    Result = Math.Sin(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])));

                    break;

                #endregion

                #region Sqrt
                case "sqrt":

                    CheckCase("Sqrt", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Sqrt() takes exactly 1 argument");

                    Result = Math.Sqrt(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])));

                    break;

                #endregion

                #region Tan
                case "tan":

                    CheckCase("Tan", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Tan() takes exactly 1 argument");

                    Result = Math.Tan(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])));

                    break;

                #endregion

                #region Truncate
                case "truncate":

                    CheckCase("Truncate", function.Identifier.Name);

                    if (function.Expressions.Length != 1)
                        throw new ArgumentException("Truncate() takes exactly 1 argument");

                    Result = Math.Truncate(Convert.ToDouble(await EvaluateAsync(function.Expressions[0])));

                    break;

                #endregion

                #region Max
                case "max":

                    CheckCase("Max", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("Max() takes exactly 2 arguments");

                    object maxleft = await EvaluateAsync(function.Expressions[0]);
                    object maxright = await EvaluateAsync(function.Expressions[1]);

                    Result = Numbers.Max(maxleft, maxright);
                    break;

                #endregion

                #region Min
                case "min":

                    CheckCase("Min", function.Identifier.Name);

                    if (function.Expressions.Length != 2)
                        throw new ArgumentException("Min() takes exactly 2 arguments");

                    object minleft = await EvaluateAsync(function.Expressions[0]);
                    object minright = await EvaluateAsync(function.Expressions[1]);

                    Result = Numbers.Min(minleft, minright);
                    break;

                #endregion

                #region if
                case "if":

                    CheckCase("if", function.Identifier.Name);

                    if (function.Expressions.Length != 3)
                        throw new ArgumentException("if() takes exactly 3 arguments");

                    bool cond = Convert.ToBoolean(await EvaluateAsync(function.Expressions[0]));

                    Result = cond ? await EvaluateAsync(function.Expressions[1]) : await EvaluateAsync(function.Expressions[2]);
                    break;

                #endregion

                #region in
                case "in":

                    CheckCase("in", function.Identifier.Name);

                    if (function.Expressions.Length < 2)
                        throw new ArgumentException("in() takes at least 2 arguments");

                    object parameter = await EvaluateAsync(function.Expressions[0]);

                    bool evaluation = false;

                    // Goes through any values, and stop whe one is found
                    for (int i = 1; i < function.Expressions.Length; i++)
                    {
                        object argument = await EvaluateAsync(function.Expressions[i]);
                        if (CompareUsingMostPreciseType(parameter, argument) == 0)
                        {
                            evaluation = true;
                            break;
                        }
                    }

                    Result = evaluation;
                    break;

                #endregion

                default:
                    throw new ArgumentException("Function not found",
                        function.Identifier.Name);
            }
            // Last result is stored in the function
            function.LastValue = Result;
        }

        private void CheckCase(string function, string called)
        {
            if (IgnoreCase)
            {
                if (function.ToLower() == called.ToLower())
                {
                    return;
                }

                throw new ArgumentException("Function not found", called);
            }

            if (function != called)
            {
                throw new ArgumentException(String.Format("Function not found {0}. Try {1} instead.", called, function));
            }
        }

        public override async Task VisitAsync(Identifier parameter)
        {
            if (Parameters.ContainsKey(parameter.Name))
            {
                // The parameter is defined in the hashtable
                if (Parameters[parameter.Name] is Expression expression)
                {
                    // The parameter is itself another Expression

                    // Overloads parameters
                    foreach (var p in Parameters)
                    {
                        expression.Parameters[p.Key] = p.Value;
                    }

                    Result = await expression.EvaluateAsync(_evaluateParameterAsync, _evaluateFunctionAsync,_time, _step, _deltaTime);
                }
                else
                    Result = Parameters[parameter.Name];
            }
            else if (parameter.Name.EndsWith("_Time"))
            {
                Result = _time;
            }
            else if (parameter.Name.EndsWith("_Dt"))
            {
                Result = _deltaTime;
            }
            else
            {
                // The parameter should be defined in a call back method
                var args = new ParameterArgs();

                if (_evaluateParameterAsync != null)
                {
                    var name = IgnoreCase ? parameter.Name.ToLower() : parameter.Name;

                    // Calls external implementations, which  may be a MulticastDelegate which 
                    // requires manual handling for async delegates.
                    foreach (var handler in _evaluateParameterAsync.GetInvocationList()
                        .Cast<EvaluateParameterAsyncHandler>())
                    {
                        await handler.Invoke(name, args);
                    }
                }

                if (!args.HasResult)
                    throw new ArgumentException("Parameter was not defined", parameter.Name);

                Result = args.Result;
            }
        }

        public Dictionary<string, object> Parameters { get; set; }
    }
}
