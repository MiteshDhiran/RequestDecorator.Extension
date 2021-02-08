using System;
using System.Threading.Tasks;
using RequestDecorator;
using RequestDecorator.Functional;

namespace CommandDecoratorExtension
{
    public interface IRequestWithFluentValidator<TI, TR, TC> : IRequest<TI, TR, TC>
    {
        new Func<Func<IRequestContext<TI, TR, TC>, Task<Result<TR>>>, Func<IRequestContext<TI, TR, TC>, Task<Result<TR>>>>
            FunctionDecorator => (Func<IRequestContext<TI, TR, TC>, Task<Result<TR>>> inputFunc) => 
                inputFunc.DecorateRequestWithFluentValidation(ValidationFunc);

        //new Task<TR> InterfaceProcess(IAPIContext<TC> context) => Task.FromResult(FunctionDecorator(ProcessRequestFunc)(new RequestContext<TI, TR, TC>(context, this)).Result
        //    .GetValueThrowExceptionIfExceptionPresent());

        Func<IRequestContext<TI, TR, TC>, MayBe<FluentValidation.ValidationException>> ValidationFunc { get; }
    }

    public static class FluentValidationExtension
    {
        public static Func<IRequestContext<TI, TR, TC>, Task<Result<TR>>>
            DecorateRequestWithFluentValidation<TI, TR, TC>(
                this Func<IRequestContext<TI, TR, TC>, Task<Result<TR>>> funcToBeDecorated,
                Func<IRequestContext<TI, TR, TC>, MayBe<FluentValidation.ValidationException>> validationFunc)
            =>
                funcToBeDecorated.PipeLineDecorateFunc<int, IRequestContext<TI, TR, TC>, Task<Result<TR>>>(
                    (input) => 0
                    , (sw, input) =>
                    {
                        var mayBeValidationException = validationFunc(input);
                        return mayBeValidationException.TryGetValue(out var validationException) 
                            ? new MayBe<Task<Result<TR>>>(Task.FromResult(new Result<TR>(validationException))) 
                            : MayBeExtension.GetNothingMaybe<Task<Result<TR>>>();
                    }
                    ,(sw, input, previousResultValue) => previousResultValue.GetValueThrowExceptionIfExceptionPresent());

        
    }

    public static class FluentRequestProcessor
    {
        public static async Task<TR> ProcessRequest<TI, TR, TC>(this IRequestWithFluentValidator<TI, TR, TC> request, IAPIContext<TC> apiContext)
        {
            var decoratedFunc = request.FunctionDecorator(request.ProcessRequestFunc)
                .DecorateWithExecutionTimeLogger();
            var res = await decoratedFunc(new RequestWithContext<TI, TR, TC>(apiContext, request));
            var retVal = res.GetValueThrowExceptionIfExceptionPresent();
            return retVal;
        }
    }



}
