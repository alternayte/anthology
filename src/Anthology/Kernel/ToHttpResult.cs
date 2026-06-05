using Microsoft.AspNetCore.Http.HttpResults;

namespace Anthology.Kernel;

public static class ResultExtensions
{
    public static Microsoft.AspNetCore.Http.IResult ToHttpResult<T>(this Result<T> result) =>
        result.Match(
            ok => Results.Ok(ok),
            err => err.Kind switch
            {
                ErrorKind.NotFound => TypedResults.Problem(err.Message, statusCode: 404, title: err.Code),
                ErrorKind.Conflict => TypedResults.Problem(err.Message, statusCode: 409, title: err.Code),
                ErrorKind.Forbidden => TypedResults.Problem(err.Message, statusCode: 403, title: err.Code),
                ErrorKind.Validation when err.ValidationErrors is not null =>
                    TypedResults.ValidationProblem(err.ValidationErrors),
                ErrorKind.Validation => TypedResults.Problem(err.Message, statusCode: 400, title: err.Code),
                _ => TypedResults.Problem(err.Message, statusCode: 422, title: err.Code),
            });

    public static async Task<Microsoft.AspNetCore.Http.IResult> ToHttpResult<T>(this Task<Result<T>> task) =>
        (await task).ToHttpResult();
}
