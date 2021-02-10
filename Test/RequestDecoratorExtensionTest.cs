using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using CommandDecoratorExtension;
using FluentValidation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using RequestDecorator.Functional;

namespace RequestDecorator.Extension.Test
{
    [TestClass]
    public class RequestDecoratorExtensionTest
    {

        public APIContext<Context> GetAPIContext()
        {
            var apiContext = new APIContext<Context>(new Context(), SerializationHelper.CustomGetJSONSerializedObject
                , (l) =>
                {
                    Console.WriteLine(l.ToString());
                }, (l) =>
                {
                    Console.WriteLine(l.ToString());
                });
            return apiContext;
        }
        [TestMethod]
        public void ResultGeneratedWhenDataIsValid()
        {
            var apiContext = GetAPIContext();
            var  req = new QueryRequest(new QueryData(1));
            var res =  req.Process(apiContext);
            Assert.IsTrue(res.Result.ID == req.Data.ID);
        }

        [TestMethod]
        public void ValidationExceptionIsThrownWhenDataIsInvalid()
        {
            var apiContext = GetAPIContext();
            var req = new QueryRequest(new QueryData(null));
            try
            {
                var res = req.Process(apiContext).Result;
                Assert.Fail(
                    "Validation exception should be raised as null value is assigned to ID property of Query Data");
            }
            catch (System.AggregateException agg)
            {
                Assert.IsTrue(agg.InnerException  is  FluentValidation.ValidationException);
            }
        }

        [TestMethod]
        public void QueryRequestDerivedFromBase_ValidationExceptionIsThrownWhenDataIsInvalid()
        {
            var apiContext = GetAPIContext();
            var req = new QueryRequestDerivedFromBase(new QueryData(null));
            try
            {
                var res = req.Process(apiContext).Result;
                Assert.Fail(
                    "Validation exception should be raised as null value is assigned to ID property of Query Data");
            }
            catch (System.AggregateException agg)
            {
                Assert.IsTrue(agg.InnerException is FluentValidation.ValidationException);
            }
        }
    }

    [DataContract]
    public class QueryData
    {
        public QueryData(int? id)
        {
            ID = id;
        }

        [DataMember]
        public int? ID { get;  }
    }

    [DataContract]
    public class Model
    {
        
        public Model(int id)
        {
            ID = id;
        }

        public int ID { get; }
    }

    public class Context
    {
        
    }

    [DataContract]
    public class QueryRequest : IRequestWithFluentValidator<QueryData,Model,Context>
    {
        public QueryRequest(QueryData data)
        {
            Data = data;
        }
        public Func<IRequestContext<QueryData, Model, Context>, MayBe<ValidationException>> ValidationFunc => (reqContext) 
            => reqContext.RequestInfo.Data.ID.HasValue
            ? new MayBe<FluentValidation.ValidationException>(MayBeDataState.DataNotPresent)
            :
            new MayBe<FluentValidation.ValidationException>(new FluentValidation.ValidationException("ID Value is null"));
        public QueryData Data { get; }

        public Func<IRequestContext<QueryData, Model, Context>, Task<Result<Model>>> ProcessRequestFunc =>
            (r) =>
            {
                var model = new Model(r.RequestInfo.Data.ID ?? -1);
                return Task.FromResult(new Result<Model>(model));
            }; 
        public async Task<Model> Process(IAPIContext<Context> context)
        {
            var decoratedFunc = this.ProcessRequestFunc.DecorateRequestWithFluentValidation(this.ValidationFunc).DecorateRequestWithInputOutputLogging(SerializationHelper.CustomGetJSONSerializedObject).DecorateWithExecutionTimeLogger();
            var res = await decoratedFunc(new RequestWithContext<QueryData, Model, Context>(context, this));
            var retVal = res.GetValueThrowExceptionIfExceptionPresent();
            return retVal;
        }
    }

    /// <summary>
    /// Base class which contains default process function which decorates the main function with various decorators
    /// </summary>
    /// <typeparam name="TI"></typeparam>
    /// <typeparam name="TR"></typeparam>
    /// <typeparam name="TC"></typeparam>

    public abstract class RequestWithValidationBase<TI,TR,TC> : IRequestWithFluentValidator<TI, TR, TC>
    {
        protected RequestWithValidationBase(TI data)
        {
            Data = data;
        }
        [JsonIgnore]
        public abstract Func<IRequestContext<TI, TR, TC>, MayBe<ValidationException>> ValidationFunc { get; }
        public TI Data { get; }
        [JsonIgnore]
        public abstract Func<IRequestContext<TI, TR, TC>, Task<Result<TR>>> ProcessRequestFunc { get; }

        public async Task<TR> Process(IAPIContext<TC> context)
        {
            var decoratedFunc = this.ProcessRequestFunc.DecorateRequestWithFluentValidation(this.ValidationFunc).DecorateRequestWithInputOutputLogging(SerializationHelper.CustomGetJSONSerializedObject).DecorateWithExecutionTimeLogger();
            var res = await decoratedFunc(new RequestWithContext<TI, TR, TC>(context, this));
            var retVal = res.GetValueThrowExceptionIfExceptionPresent();
            return retVal;
        }
    }

    public class QueryRequestDerivedFromBase : RequestWithValidationBase<QueryData,Model,Context>
    {
        public QueryRequestDerivedFromBase(QueryData data) : base(data)
        {
        }

       // [JsonIgnore]
        public override Func<IRequestContext<QueryData, Model, Context>, MayBe<ValidationException>> ValidationFunc =>
            (IRequestContext<QueryData, Model, Context>  req) =>
                req.RequestInfo.Data.ID.HasValue
                    ? new MayBe<FluentValidation.ValidationException>(MayBeDataState.DataNotPresent)
                    :
                    new MayBe<FluentValidation.ValidationException>(new FluentValidation.ValidationException("ID Value is null"));

        //[JsonIgnore]
        public override Func<IRequestContext<QueryData, Model, Context>, Task<Result<Model>>> ProcessRequestFunc
        =>
            (r) =>
            {
                var model = new Model(r.RequestInfo.Data.ID ?? -1);
                return Task.FromResult(new Result<Model>(model));
            };
    }

    public static class SerializationHelper
    {
        public static string CustomGetJSONSerializedObject(object value)
        {
            try
            {
                if (value == null)
                    return string.Empty;
                var sb = new StringBuilder();
                var serializer = new JsonSerializer();
                serializer.Serialize(new StringWriter(sb), value);
                return sb.ToString();
            }
            catch(Exception ex)
            {
                return $"Could not serialize. Error: {ex.Message}";
            }
            return "Could not serialize";
        }
    }
}
