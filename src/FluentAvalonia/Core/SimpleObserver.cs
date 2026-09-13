namespace FluentAvalonia.Core;

internal class SimpleObserver<T>(Action<T> listener) : IObserver<T>
{
    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void OnNext(T value) => listener(value);
}

internal static class ReactiveExtensions
{
    extension<T>(IObservable<T> source)
    {
        public IDisposable Subscribe(Action<T> subAction) =>
            source.Subscribe(new SimpleObserver<T>(subAction));

        public IObservable<T> Skip(int skipCount)
        {
            return Create<T>(obs =>
            {
                var remaining = skipCount;
                return source.Subscribe(new SimpleObserver<T>(input =>
                {
                    if (remaining <= 0)
                        obs.OnNext(input);
                    else
                        remaining--;
                }));
            });
        }
    }

    public static IObservable<TSource> Create<TSource>(Func<IObserver<TSource>, IDisposable> subscribe)
    {
        return new CreateWithDisposableObservable<TSource>(subscribe);
    }

    private sealed class CreateWithDisposableObservable<TSource>(Func<IObserver<TSource>, IDisposable> subscribe) : IObservable<TSource>
    {
        public IDisposable Subscribe(IObserver<TSource> observer)
        {
            return subscribe(observer);
        }
    }
}