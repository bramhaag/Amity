# Amity
Amity is a utility to create simple patches to exitsing assemblies. Inspired by [Harmony](https://github.com/pardeike/Harmony)

## Examples
### The assembly
```csharp
public class MyClass
{
    public void MyMethod() 
    {
        Console.WriteLine("Hello, World!");
    }
}
```
This assembly is saved in `C:\MyAssembly.dll`

### The patch
```csharp
public class MyPatch {
    [AmityPatch(typeof(MyClass), "MyMethod", AmityPatch.Mode.Prefix)]
    public static void Patch() 
    {
        Console.WriteLine("Hello from Amity!");
    }
}
```


### Applying the patch
```csharp
public class Program
{
    public static void Main()
    {
        AmityInstance.Patch(typeof(MyPatch), @"C:\MyAssembly.dll", @"C:\MyNewAssembly.dll");
    }
}
```

Running this will generate the patched assembly in `C:\MyNewAssembly.dll`, the `MyMethod()` method in this assembly now looks like this:
```csharp
public void MyMethod() 
{
    Console.WriteLine("Hello from Amity!");
    Console.WriteLine("Hello, World!");
}
```