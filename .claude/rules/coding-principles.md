## Coding Principles

### 1. **SOLID**
- **Single Responsibility**: Each class should have only one reason to change
- **Open/Closed**: Create entities that are open for extension but closed to modification
- **Liskov Substitution**: Subtypes must be substitutable for base types
- **Interface Segregation**: Create "thin", focused interfaces
- **Dependency Inversion**: Depend on abstractions, not concrete implementations

### 2. **Keep it Stupid Simple (KISS)**
- **Simplicity**: Favor simple solutions over complex ones
- **Avoid Premature Optimization**: Focus on clear, maintainable code first
- **Readability**: Write code that is easy to understand and maintain

### 3. **Don't Repeat Yourself (DRY)**
- **Avoid Duplication**: Reuse code through abstraction
- **Once and Only Once**: Define logic in a single place

### 4. **Favor Composition Over Inheritance**
- **Limit Inheritance Depth**: Prefer flat structures with clear interfaces
- **Prefer Interfaces**: Define contracts through interfaces, not abstract classes
- **Use Aggregation**: Combine multiple services to create complex behavior
- **Avoid Fragile Base Class**: Don't rely on base class implementation details

### 5. **Principle of Least Astonishment (POLA)**
- **Intuitive Behavior**: Code should behave in a way that is expected and familiar
- **Consistent Design**: Maintain consistent patterns and practices across the codebase
- **Organize Code**: Structure code in a way that is logical and easy to navigate
- **Follow Conventions**: Follow established platform and framework conventions