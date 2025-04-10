# QuickMapper

QuickMapper is a lightweight and flexible object mapping library for .NET projects. Designed as an alternative to AutoMapper, it offers complete control, custom configurations, and advanced support for various features.

---

## Features

### 🛠️ Main Features

- **Nested Object Support**: Automatically maps complex properties or inner objects.
- **Custom Converters**: Configure specific logic for property conversion.
- **Collections**: Native support for mapping lists and arrays.
- **Bidirectional**: Create reverse mappings automatically.
- **Pre-Mapping Validation**: Add validation rules for objects.
- **Performance Optimization**: Uses compiled expressions for maximum efficiency.
- **Flexible Configuration**: Ignore properties or set default validations.
- **Logging and Debugging**: Record mapping operations for easy monitoring.
- **Dependency Injection Support**: Fully compatible with DI frameworks.

---

## Installation

### 🖥️ How to Add to Your Project

1. Clone the repository.
2. Add the library to your project:
   - In Visual Studio, right-click on the project and select **Add Reference**.
   - Navigate to the library folder and select the file.
3. Import the namespace:
   ```csharp
   using QuickMapper;
   ```

## Usage

### Basic Mapping

```csharp
// Define your source and destination classes
public class UserDto
{
    public string Name { get; set; }
    public int Age { get; set; }
}

public class UserEntity
{
    public string Name { get; set; }
    public int Age { get; set; }
}

// Create and configure the mapper
var logger = LoggerFactory.Create(builder => builder.AddConsole())
    .CreateLogger<Mapper>();
var mapper = new Mapper(logger);
mapper.CreateMap<UserDto, UserEntity>();

// Perform the mapping
var dto = new UserDto { Name = "John Doe", Age = 30 };
var entity = mapper.Map<UserDto, UserEntity>(dto);
```

### Nested Objects

```csharp
public class AddressDto
{
    public string Street { get; set; }
    public string City { get; set; }
}

public class UserWithAddressDto
{
    public string Name { get; set; }
    public AddressDto Address { get; set; }
}

// Mapper will automatically handle nested objects
var logger = LoggerFactory.Create(builder => builder.AddConsole())
    .CreateLogger<Mapper>();
var mapper = new Mapper(logger);
mapper.CreateMap<UserWithAddressDto, UserEntity>();
```

### Custom Converters

```csharp
mapper.CreateMap<UserDto, UserEntity>();
mapper.ForMember<UserDto, UserEntity, string>(
    dest => dest.FullName,
    src => $"{((UserDto)src).FirstName} {((UserDto)src).LastName}"
);
```

### Collection Mapping

```csharp
public class OrderDto
{
    public List<ItemDto> Items { get; set; }
}

public class Order
{
    public ICollection<Item> Items { get; set; }
}

// Lists and arrays are automatically mapped
mapper.CreateMap<OrderDto, Order>();
mapper.CreateMap<ItemDto, Item>();
```

### Bidirectional Mapping

```csharp
// Create mappings in both directions
mapper.CreateMap<UserDto, UserEntity>();
mapper.CreateReverseMap<UserDto, UserEntity>();
```

### Validation

```csharp
mapper.AddValidator(src => 
{
    if (src is UserDto dto && string.IsNullOrEmpty(dto.Name))
        return false;
    return true;
});
mapper.CreateMap<UserDto, UserEntity>();
```

### Logging

```csharp
// Enable logging (requires Microsoft.Extensions.Logging)
var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole());
var logger = loggerFactory.CreateLogger<Mapper>();

var mapper = new Mapper(logger); // Logger is required
```

### Dependency Injection Setup

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IMapper>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<Mapper>>();
        var mapper = new Mapper(logger);
        // Configure your mappings
        mapper.CreateMap<UserDto, UserEntity>();
        return mapper;
    });
}
```

## Todo

  - [ ] Code cleanup
  - [ ] Add more unit tests
  - [ ] Publish as a NuGet package
