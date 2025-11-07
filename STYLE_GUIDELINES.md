Walt’s C# Style Guide

## Naming

* Namespaces, Types (class, struct, record, interface, enum), Members, Methods, Properties, Fields, Events, Delegates: PascalCase always, even if private
* Parameters and local variables: camelCase
* No underscores in any identifier.
* Interfaces: PascalCase with I prefix not required but recommended
* Generic type parameters: TSomething (PascalCase, leading T not required but recommended)
* Constants and static readonly fields: PascalCase
* Enums and enum members: PascalCase, for <=256 elements use byte as the underlying type
* Events: no prefix, event args types end with EventArgs; for the raiser methods, prefix with On
* Callback interfaces end with Callbacks, use these in places with more than one callback, otherwise use a delegate.
* Tuple element names: camelCase

## Files, layout, and ordering

* No file per type rule, you can have as many or as little types as you want in files, and naming doesn't have to follow the file name
* Folder structure can follow namespace structure, but doesn't have to
* Explicit access modifiers everywhere
* No regions
* No comments of any kind, including XML docs
* Sorry to the people who care about IntelliSense

## Formatting

* Indent with tabs or 4 spaces
* Do not use braces for single statements, design the code with this in mind, aiming to use single statement ifs/fors/whatever where possible
* You can have many layers of these so long as there is no ambiguity and the code runs correctly
* No blank lines required between anything, but sometimes good to break up different sets of members
* Spacing: single space after commas and around binary operators; no space before semicolons; no spaces just inside parentheses

## Usings and qualification

* Place usings at file top, outside namespace
* The order of usings does not matter
* Use static usings commonly, but only alias usings for ambiguity
* Fully qualify types only to resolve ambiguity, and if there is more than one instance of ambiguous types use an alias using
* Use global usings for things that are used in large parts of the codebase

## Expressions and literals

* Prefer interpolated strings to concatenation or string.Format
* Verbatim strings for paths
* Do not use digit separators in long numeric literals, just get better at reading
* Null checks use x == null, not pattern x is null
* Prefer early returns to deep nesting
* Default values: use default or default(T) when what the default is for isn't obvious, and default literal in assignments when the type is clear

## Object creation and collection literals

* Do not use var, except in foreach statements where the type is obvious
* Avoid anonymous types
* Use target typed new only where the intended type is obvious and not in argument lists
* Correct:
* ```csharp
  List<int> numbers = [];
  Vector4 vector = new(1, 2, 3, 4);
  ```
* Not allowed:
* ```csharp
  DoThing(new(), [1, 2, 3]);
  ```
* Allowed alternative in arguments:
* ```csharp
  DoThing(new Vector4(1, 2, 3, 4), new List<int>(1, 2, 3, 4));
  ```
* Collection expressions where supported but not in arguments
* List: `List<int> numbers = [1, 2, 3];`
* HashSet: `HashSet<string> set = ["a", "b"];`
* Dictionary:
* ```csharp
  Dictionary<string, int> map = [
      new("a", 1),
      new("b", 2)
  ];

  ```
* Use array literals with explicit type when needed:
* ```csharp
  int[] values = [1, 2, 3];
  ```

## LINQ and queries

* Use method syntax, not the special word syntax
* Break long chains across lines before the dots

## Methods and signatures

* Parameters are camelCase; put optional parameters after required ones
* CancellationTokens have no specific order rules
* For outputting many values, use the following order to decide
  * tuple (<= 3 values)
  * out parameters (<= 5 values)
  * special type (>5 values)

## Exceptions and errors

* Throw specific exception types, and if there isn't one use Exception or a custom one
* Parameter names in argument related exceptions are explicit strings, not nameof
* Guard clause style for argument validation:
* ```csharp
  if (path == null)
      throw new ArgumentNullException("path", "Path is null.");
  if (path.Length == 0)
      throw new ArgumentException("Path must not be empty.", "path");
  ```

## Properties and fields

* Auto properties where possible
* Backing fields are PascalCase with the suffix Internal, for example:
* ```csharp
  private string NameInternal;
  public string Name => NameInternal;
  ```
* Use readonly for fields that never change after construction

## Async and tasks

* Methods that perform async work return Task or Task `<T>` but do not have to end with Async
* Avoid async void except for event handlers

## Pattern matching and switches

* Use switch expressions for simple value transformations that read clearly
* Use switch statements when multiple statements are required per case
* Include a default case when the domain is not fully covered

## Equality, comparison, hashing and disposing

* Prefer records for value based equality but not for disposables
* Use record structs where needed
* Anything which uses file handles or things that need to be freed should be IDisposable and free the things in Dispose
* I like to give static classes a Dispose or similarly named method to be called from other clean up parts of the code, no set in stone rules around this I just like to do it and it's useful
* Use destructors sparingly

## Immutability and records

* Prefer record types for their benefits:
  * ToString
  * Equality
  * Immutability
* But not for disposables
* Records, classes, and structs all follow the same naming and formatting rules

## Nullable and warnings

* Enable nullable reference types for all projects
* Treat warnings as errors in CI
* Use of the damnit operator `!` is not discouraged, as long as it won't cause issues
* Use of the following code:
* ```
  public static Example Example = null!;
  ```
* is encouraged for static (or instance) defaults that won't be set in the constructor but in some initializing function or external source
* You can just pray that it won't be used before the initialization, and if you see an NullReferenceException you'll know where it came from
* Use explicit nullability annotations in public APIs
