namespace ProfileMicroservice.Entities;

public interface IProfile<T, U>
{
    U Convert(T profile);
    T Convert(U profile);
}
