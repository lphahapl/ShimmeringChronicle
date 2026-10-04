/// <summary>Provides containers. Unhandled kinds fall back to the object's default container.</summary>
public interface IContainerOwner
{
    IItemOperator GetContainer(ContainerKind kind = ContainerKind.Default);
}
public enum ContainerKind
{
    Default,
    Bag,
    QuickBar,

}
