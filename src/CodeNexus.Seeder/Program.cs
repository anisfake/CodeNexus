using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

static class Program
{
    // Cross-cutting goals that apply to multiple subjects
    private static readonly Dictionary<string, string[]> CrossCuttingGoals = new()
    {
        // Full-stack development goals
        ["Build Full-Stack Web Application"] = new[] { "React", "Vue", "Angular", "Next.js", "Node.js", "Express.js", "ASP.NET Core", "Django", "Laravel", "Spring Boot", "FastAPI" },
        ["Master REST API Development"] = new[] { "Node.js", "Express.js", "ASP.NET Core", "Django", "Laravel", "Spring Boot", "FastAPI", "Go", "Python", "C#", "Java" },
        ["Learn Authentication & Authorization"] = new[] { "Node.js", "Express.js", "ASP.NET Core", "Django", "Laravel", "Spring Boot", "FastAPI", "React", "Vue", "Angular", "Next.js" },
        
        // Database & persistence goals
        ["Master Database Design & Optimization"] = new[] { "SQL Server", "MySQL", "PostgreSQL", "SQLite", "MongoDB", "Node.js", "ASP.NET Core", "Django", "Laravel", "Spring Boot" },
        ["Learn Data Modeling & Relationships"] = new[] { "SQL Server", "MySQL", "PostgreSQL", "SQLite", "MongoDB", "Django", "Laravel", "Spring Boot", "ASP.NET Core" },
        
        // DevOps & deployment goals
        ["Master CI/CD Pipeline"] = new[] { "Node.js", "React", "Vue", "Angular", "ASP.NET Core", "Django", "Laravel", "Spring Boot", "Python", "C#", "Java", "Go" },
        ["Learn Containerization with Docker"] = new[] { "Node.js", "ASP.NET Core", "Django", "Laravel", "Spring Boot", "Python", "C#", "Java", "Go", "Linux" },
        ["Deploy Applications to Cloud"] = new[] { "Azure", "Google Cloud", "Node.js", "ASP.NET Core", "Django", "Laravel", "Spring Boot", "React", "Vue", "Angular" },
        
        // Testing & quality goals
        ["Master Unit Testing & TDD"] = new[] { "C#", "Java", "Python", "JavaScript", "TypeScript", "Go", "React", "Vue", "Angular", "Node.js", "ASP.NET Core", "Django", "Spring Boot" },
        ["Learn Integration Testing"] = new[] { "Node.js", "Express.js", "ASP.NET Core", "Django", "Laravel", "Spring Boot", "FastAPI", "React", "Vue", "Angular" },
        ["Implement Code Quality & Reviews"] = new[] { "C#", "Java", "Python", "JavaScript", "TypeScript", "Go", "Rust", "C++", "React", "Vue", "Angular", "Node.js" },
        
        // Performance & optimization goals
        ["Master Performance Optimization"] = new[] { "React", "Vue", "Angular", "Node.js", "ASP.NET Core", "Django", "Spring Boot", "C#", "Java", "Python", "Go", "C++" },
        ["Learn Caching Strategies"] = new[] { "Node.js", "ASP.NET Core", "Django", "Laravel", "Spring Boot", "FastAPI", "SQL Server", "MySQL", "PostgreSQL", "MongoDB" },
        ["Optimize Database Queries"] = new[] { "SQL Server", "MySQL", "PostgreSQL", "SQLite", "MongoDB", "Node.js", "ASP.NET Core", "Django", "Laravel", "Spring Boot" },
        
        // Security goals
        ["Implement Web Security Best Practices"] = new[] { "Node.js", "Express.js", "ASP.NET Core", "Django", "Laravel", "Spring Boot", "FastAPI", "React", "Vue", "Angular", "Cybersecurity" },
        ["Learn Secure Coding Practices"] = new[] { "C#", "Java", "Python", "JavaScript", "TypeScript", "Go", "PHP", "Ruby", "Cybersecurity", "ASP.NET Core", "Django", "Spring Boot" },
        
        // Architecture & design goals
        ["Master Clean Architecture"] = new[] { "C#", "Java", "Python", "Go", "ASP.NET Core", "Spring Boot", "Django", "FastAPI", "Design Patterns" },
        ["Learn Domain-Driven Design"] = new[] { "C#", "Java", "Python", "Go", "ASP.NET Core", "Spring Boot", "Django", "Design Patterns", "Microservices" },
        ["Implement SOLID Principles"] = new[] { "C#", "Java", "Python", "TypeScript", "Go", "Design Patterns", "ASP.NET Core", "Spring Boot", "Django" },
        
        // Mobile development goals
        ["Build Cross-Platform Mobile App"] = new[] { "Flutter", "React Native", "Kotlin", "Swift" },
        ["Master Mobile UI/UX Design"] = new[] { "Flutter", "React Native", "Android", "IOS", "Swift", "Kotlin" },
        ["Implement Mobile App Security"] = new[] { "Flutter", "React Native", "Android", "IOS", "Swift", "Kotlin", "Cybersecurity" },
        
        // Data science & ML goals
        ["Build End-to-End ML Pipeline"] = new[] { "Python", "Machine Learning", "PyTorch", "TensorFlow", "Pandas", "NumPy", "Data Analysis" },
        ["Master Data Visualization"] = new[] { "Python", "Pandas", "NumPy", "Power BI", "Tableau", "Data Analysis" },
        ["Learn MLOps & Model Deployment"] = new[] { "Python", "Machine Learning", "PyTorch", "TensorFlow", "Azure", "Google Cloud" },
        
        // Game development goals
        ["Create Complete Game Project"] = new[] { "Unity", "Godot", "Game Development", "C#" },
        ["Master Game Physics & Mechanics"] = new[] { "Unity", "Godot", "Game Development", "C#", "C++" },
        
        // Blockchain & Web3 goals
        ["Build Decentralized Application (DApp)"] = new[] { "Blockchain", "JavaScript", "TypeScript", "React", "Vue" },
        ["Master Smart Contract Development"] = new[] { "Blockchain", "JavaScript", "TypeScript" },
        
        // General programming goals
        ["Master Object-Oriented Programming"] = new[] { "C#", "Java", "Python", "C++", "Kotlin", "Swift", "TypeScript" },
        ["Learn Functional Programming"] = new[] { "JavaScript", "TypeScript", "Python", "C#", "Rust", "Go" },
        ["Master Async Programming"] = new[] { "C#", "JavaScript", "TypeScript", "Python", "Node.js", "ASP.NET Core", "Go", "Rust" },
        ["Learn Memory Management"] = new[] { "C++", "Rust", "C#", "Java", "Go", "Swift" },
        
        // Leadership & soft skills goals
        ["Lead Technical Team"] = new[] { "C#", "Java", "Python", "JavaScript", "TypeScript", "Go", "Design Patterns", "Microservices" },
        ["Master Code Review Process"] = new[] { "C#", "Java", "Python", "JavaScript", "TypeScript", "Go", "React", "Vue", "Angular" },
        ["Learn Technical Documentation"] = new[] { "C#", "Java", "Python", "JavaScript", "TypeScript", "Go", "React", "Vue", "Angular", "Node.js" }
    };

    private static readonly Dictionary<string, string[]> SubjectGoalsMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Mobile Development
            ["IOS"] = new[]
            {
                "Master iOS App Development Fundamentals",
                "Build iOS Apps with SwiftUI & UIKit",
                "Implement iOS Networking & Data Persistence",
                "Master iOS UI/UX Design Patterns",
                "Publish Professional iOS App to App Store",
                "Optimize iOS App Performance & Memory"
            },
            ["Swift"] = new[]
            {
                "Master Swift Programming Fundamentals",
                "Learn Swift Concurrency & Async/Await",
                "Build iOS Applications with Swift",
                "Optimize Swift Performance & Memory Management",
                "Master Swift Design Patterns & Best Practices"
            },
            ["Android"] = new[]
            {
                "Master Android App Development Fundamentals",
                "Build Modern Android Apps with Jetpack Compose",
                "Implement Android Architecture Components (MVVM)",
                "Master Android Networking & Local Storage",
                "Publish Professional Android App to Play Store",
                "Optimize Android App Performance & Battery Life"
            },
            ["Kotlin"] = new[]
            {
                "Master Kotlin Programming Fundamentals",
                "Learn Kotlin Coroutines & Flow",
                "Build Android Applications with Kotlin",
                "Develop Backend Services with Kotlin (Ktor)",
                "Master Kotlin Multiplatform Development"
            },
            ["Flutter"] = new[]
            {
                "Master Flutter App Development Fundamentals",
                "Implement Advanced Flutter State Management",
                "Build Beautiful Flutter UI with Animations",
                "Master Flutter Performance Optimization",
                "Publish Cross-Platform Flutter App",
                "Integrate Flutter with Native Platform Features"
            },
            ["React Native"] = new[]
            {
                "Master React Native Development Fundamentals",
                "Implement Mobile Navigation & State Management",
                "Build Native Modules & Platform-Specific Features",
                "Optimize React Native App Performance",
                "Publish React Native App to Both Stores",
                "Master React Native Testing & Debugging"
            },

            // Backend Programming Languages
            ["Python"] = new[]
            {
                "Master Python Programming Fundamentals",
                "Build Data Processing Workflows with Python",
                "Develop REST APIs with Python Frameworks",
                "Deploy Production-Ready Python Applications",
                "Master Python for Data Science & Machine Learning",
                "Implement Python Testing & Code Quality"
            },
            ["C#"] = new[]
            {
                "Master C# Programming Fundamentals",
                "Learn Object-Oriented Programming with C#",
                "Explore .NET Ecosystem & Libraries",
                "Build Backend Services with C#",
                "Master C# Async Programming & Performance",
                "Implement C# Testing & Best Practices"
            },
            ["Java"] = new[]
            {
                "Master Java Programming Fundamentals",
                "Learn Java OOP & Collections Framework",
                "Build Enterprise Java Backend Applications",
                "Optimize Java Performance & Memory Management",
                "Master Java Concurrency & Multithreading",
                "Implement Java Testing & Design Patterns"
            },
            ["Go"] = new[]
            {
                "Master Go Programming Fundamentals",
                "Learn Go Concurrency & Goroutines",
                "Build High-Performance APIs with Go",
                "Deploy Production-Ready Go Applications",
                "Master Go Testing & Benchmarking",
                "Implement Go Microservices Architecture"
            },
            ["Rust"] = new[]
            {
                "Master Rust Programming Fundamentals",
                "Learn Ownership, Borrowing & Memory Safety",
                "Build Systems Programming Projects with Rust",
                "Develop Backend Services with Rust",
                "Master Rust Performance & Optimization",
                "Implement Rust Testing & Error Handling"
            },
            ["C++"] = new[]
            {
                "Master C++ Programming Fundamentals",
                "Learn Modern C++ (C++17/20) Features & STL",
                "Optimize C++ Performance & Memory Management",
                "Build Systems Programming with C++",
                "Master C++ Design Patterns & Best Practices",
                "Implement C++ Testing & Debugging"
            },
            ["Ruby"] = new[]
            {
                "Master Ruby Programming Fundamentals",
                "Learn Ruby OOP & Metaprogramming",
                "Build Web Applications with Ruby on Rails",
                "Deploy Production-Ready Ruby Applications",
                "Master Ruby Testing & Code Quality",
                "Implement Ruby Performance Optimization"
            },
            ["PHP"] = new[]
            {
                "Master PHP Programming Fundamentals",
                "Learn Modern PHP (OOP, Composer & PSR Standards)",
                "Build Dynamic Web Applications with PHP",
                "Implement PHP Security Best Practices",
                "Master PHP Framework Development",
                "Deploy Production-Ready PHP Applications"
            },

            // Backend Frameworks
            ["Node.js"] = new[]
            {
                "Master Node.js Runtime Fundamentals",
                "Build RESTful APIs with Node.js",
                "Master Async Programming & Event Loop",
                "Deploy Production-Ready Node.js Applications",
                "Implement Node.js Testing & Debugging",
                "Optimize Node.js Performance & Scalability"
            },
            ["Express.js"] = new[]
            {
                "Master Express.js Framework Fundamentals",
                "Design RESTful APIs with Express.js",
                "Implement Middleware & Security in Express",
                "Build Real-time Applications with Express",
                "Master Express.js Testing & Error Handling",
                "Deploy Express.js Applications to Production"
            },
            ["Django"] = new[]
            {
                "Master Django Framework Fundamentals",
                "Build Django REST APIs & Web Applications",
                "Master Django ORM & Database Migrations",
                "Implement Django Authentication & Security",
                "Deploy Django Applications to Production",
                "Master Django Testing & Performance Optimization"
            },
            ["FastAPI"] = new[]
            {
                "Master FastAPI Framework Fundamentals",
                "Build High-Performance Async APIs with FastAPI",
                "Implement Data Validation & OpenAPI Documentation",
                "Master FastAPI Authentication & Security",
                "Deploy FastAPI Applications to Production",
                "Implement FastAPI Testing & Monitoring"
            },
            ["Laravel"] = new[]
            {
                "Master Laravel Framework Fundamentals",
                "Build Web Applications with Laravel Eloquent ORM",
                "Develop Laravel API & Microservices",
                "Implement Laravel Authentication & Authorization",
                "Deploy Laravel Applications to Production",
                "Master Laravel Testing & Performance Optimization"
            },
            ["Spring Boot"] = new[]
            {
                "Master Spring Boot Framework Fundamentals",
                "Build Enterprise REST APIs with Spring Boot",
                "Implement Spring Data JPA & Database Integration",
                "Master Spring Security & Authentication",
                "Deploy Spring Boot Applications to Production",
                "Implement Spring Boot Testing & Monitoring"
            },
            ["ASP.NET Core"] = new[]
            {
                "Master ASP.NET Core Framework Fundamentals",
                "Build RESTful APIs with ASP.NET Core",
                "Implement Authentication & Authorization in .NET",
                "Master Entity Framework Core & Database Design",
                "Deploy ASP.NET Core Applications to Production",
                "Implement ASP.NET Core Testing & Performance"
            },

            // Frontend Frameworks
            ["Next.js"] = new[]
            {
                "Master Next.js Framework Fundamentals",
                "Implement SSR, SSG & ISR with Next.js",
                "Build Full-Stack Applications with Next.js API Routes",
                "Optimize Next.js Performance & SEO",
                "Deploy Next.js Applications to Production",
                "Master Next.js Testing & Best Practices"
            },
            ["React"] = new[]
            {
                "Master React Library Fundamentals",
                "Implement Advanced State Management (Redux/Zustand)",
                "Optimize React Performance & Component Architecture",
                "Master React Hooks & Custom Hook Development",
                "Implement Comprehensive React Testing",
                "Build Production-Ready React Applications"
            },
            ["Vue"] = new[]
            {
                "Master Vue.js Framework Fundamentals",
                "Implement Vue State Management with Pinia",
                "Build Single Page Applications with Vue Router",
                "Optimize Vue Performance & Component Design",
                "Master Vue Composition API & Reactivity",
                "Implement Vue Testing & Best Practices"
            },
            ["Angular"] = new[]
            {
                "Master Angular Framework Fundamentals",
                "Implement Reactive Programming with RxJS",
                "Build Complex Angular Forms & Validation",
                "Optimize Angular Performance & Change Detection",
                "Master Angular Testing & Dependency Injection",
                "Deploy Angular Applications to Production"
            },

            // Frontend Technologies
            ["HTML & CSS"] = new[]
            {
                "Master HTML5 & Semantic Web Fundamentals",
                "Build Responsive Web Design with CSS3",
                "Implement Modern CSS Layouts (Grid & Flexbox)",
                "Create Accessible UI Components",
                "Master CSS Animations & Transitions",
                "Optimize Web Performance & Core Web Vitals"
            },
            ["Tailwind CSS"] = new[]
            {
                "Master Tailwind CSS Utility-First Fundamentals",
                "Build Design Systems with Tailwind CSS",
                "Create Responsive Interfaces with Tailwind",
                "Customize Tailwind for Production Applications",
                "Master Tailwind Component Patterns",
                "Optimize Tailwind CSS Performance"
            },
            ["TypeScript"] = new[]
            {
                "Master TypeScript Language Fundamentals",
                "Learn Advanced Types, Generics & Utility Types",
                "Implement TypeScript in Frontend Applications",
                "Build Backend Services with TypeScript",
                "Master TypeScript Configuration & Tooling",
                "Implement TypeScript Testing & Best Practices"
            },
            // Architecture & Design
            ["Design Patterns"] = new[]
            {
                "Master Core Design Patterns (Creational, Structural, Behavioral)",
                "Implement SOLID Principles & Clean Code Practices",
                "Apply Design Patterns in Real-World Projects",
                "Master Refactoring Techniques with Patterns",
                "Learn Enterprise Architecture Patterns",
                "Implement Domain-Driven Design Patterns"
            },
            ["Microservices"] = new[]
            {
                "Master Microservices Architecture Fundamentals",
                "Implement Service Communication & API Gateway",
                "Build Observability & Monitoring for Microservices",
                "Master Microservices Resilience & Fault Tolerance",
                "Deploy Microservices with Container Orchestration",
                "Implement Microservices Security & Testing"
            },

            // Security & DevOps
            ["Cybersecurity"] = new[]
            {
                "Master Information Security Fundamentals",
                "Implement Web Application Security Best Practices",
                "Learn Threat Modeling & Risk Assessment",
                "Master Secure Software Development Lifecycle",
                "Implement Security Testing & Vulnerability Assessment",
                "Learn Incident Response & Security Monitoring"
            },
            ["Linux"] = new[]
            {
                "Master Linux Operating System Fundamentals",
                "Learn Command Line Interface & Shell Scripting",
                "Configure Linux for Server Administration",
                "Implement DevOps Practices on Linux",
                "Master Linux Security & Performance Tuning",
                "Build Linux-based Development Environment"
            },

            // Computer Science Fundamentals
            ["Data Structures"] = new[]
            {
                "Master Core Data Structures (Arrays, Lists, Trees, Graphs)",
                "Implement Data Structures in Programming Projects",
                "Analyze Performance & Time/Space Complexity",
                "Solve Complex Problems with Data Structures",
                "Master Advanced Data Structures (Heaps, Tries, B-Trees)",
                "Optimize Data Structure Selection for Applications"
            },
            ["Algorithms"] = new[]
            {
                "Develop Algorithmic Thinking & Problem-Solving Skills",
                "Master Sorting & Searching Algorithms",
                "Implement Graph Algorithms & Tree Traversals",
                "Learn Dynamic Programming & Optimization Techniques",
                "Master Algorithm Analysis & Complexity Theory",
                "Solve Competitive Programming Challenges"
            },

            // Data Science & Analytics
            ["Data Analysis"] = new[]
            {
                "Master Data Analysis Fundamentals & Statistics",
                "Learn Data Cleaning & Preprocessing Techniques",
                "Perform Exploratory Data Analysis & Visualization",
                "Master Statistical Analysis & Hypothesis Testing",
                "Implement Data Storytelling & Communication",
                "Build End-to-End Data Analysis Projects"
            },
            ["Pandas"] = new[]
            {
                "Master Pandas Library Fundamentals",
                "Implement Advanced Data Cleaning with Pandas",
                "Analyze Time Series Data with Pandas",
                "Optimize Pandas Performance for Large Datasets",
                "Master Pandas Data Visualization Integration",
                "Build Data Processing Pipelines with Pandas"
            },
            ["NumPy"] = new[]
            {
                "Master NumPy Array Operations & Broadcasting",
                "Implement Vectorization for Performance Optimization",
                "Perform Linear Algebra Operations with NumPy",
                "Use NumPy for Machine Learning Preprocessing",
                "Master NumPy Advanced Indexing & Slicing",
                "Optimize NumPy Code for Scientific Computing"
            },
            ["Power BI"] = new[]
            {
                "Master Power BI Desktop & Service Fundamentals",
                "Build Advanced Data Models in Power BI",
                "Master DAX Functions & Calculated Measures",
                "Design Interactive Dashboards & Reports",
                "Implement Power BI Security & Governance",
                "Deploy Power BI Solutions to Production"
            },
            ["Tableau"] = new[]
            {
                "Master Tableau Desktop Fundamentals",
                "Implement Advanced Data Modeling & Preparation",
                "Create Compelling Data Storytelling with Tableau",
                "Build Interactive Dashboards & Visualizations",
                "Master Tableau Server Administration",
                "Optimize Tableau Performance & Best Practices"
            },

            // Machine Learning & AI
            ["Machine Learning"] = new[]
            {
                "Master Machine Learning Fundamentals & Theory",
                "Implement Supervised Learning Algorithms",
                "Build Unsupervised Learning & Clustering Models",
                "Master ML Model Evaluation & Validation",
                "Deploy Machine Learning Models to Production",
                "Implement MLOps & Model Monitoring"
            },
            ["PyTorch"] = new[]
            {
                "Master PyTorch Framework Fundamentals",
                "Build & Train Deep Neural Networks",
                "Implement Computer Vision with PyTorch",
                "Master Natural Language Processing with PyTorch",
                "Deploy PyTorch Models to Production",
                "Optimize PyTorch Performance & Distributed Training"
            },
            ["TensorFlow"] = new[]
            {
                "Master TensorFlow Framework Fundamentals",
                "Build Neural Networks with TensorFlow/Keras",
                "Implement TensorFlow for Production Deployment",
                "Master TensorFlow Model Serving & APIs",
                "Optimize TensorFlow Performance & Scalability",
                "Build End-to-End ML Pipelines with TensorFlow"
            },
            ["Natural Language Processing"] = new[]
            {
                "Master NLP Fundamentals & Text Processing",
                "Implement Advanced Text Preprocessing Techniques",
                "Build Transformer Models & BERT Applications",
                "Master Large Language Model Fine-tuning",
                "Deploy NLP Models to Production",
                "Implement NLP Ethics & Bias Mitigation"
            },

            // Database Technologies
            ["SQL Server"] = new[]
            {
                "Master SQL Server Database Fundamentals",
                "Write Advanced T-SQL Queries & Stored Procedures",
                "Implement Database Indexing & Performance Tuning",
                "Master SQL Server Backup & Recovery Strategies",
                "Build SQL Server Integration Services (SSIS)",
                "Implement SQL Server Security & Administration"
            },
            ["MySQL"] = new[]
            {
                "Master MySQL Database Fundamentals",
                "Implement Advanced Query Optimization Techniques",
                "Design Efficient Database Schemas & Relationships",
                "Master MySQL Administration & Configuration",
                "Implement MySQL Replication & High Availability",
                "Optimize MySQL Performance & Monitoring"
            },
            ["PostgreSQL"] = new[]
            {
                "Master PostgreSQL Database Fundamentals",
                "Implement Advanced SQL & Complex Queries",
                "Master PostgreSQL Indexing & Performance Tuning",
                "Build PostgreSQL Extensions & Custom Functions",
                "Implement PostgreSQL Administration & Monitoring",
                "Master PostgreSQL Replication & Scaling"
            },
            ["SQLite"] = new[]
            {
                "Master SQLite Embedded Database Fundamentals",
                "Design Efficient SQLite Database Schemas",
                "Implement SQLite Transactions & Concurrency",
                "Optimize SQLite Performance for Applications",
                "Master SQLite Integration in Mobile Apps",
                "Implement SQLite Backup & Migration Strategies"
            },
            ["MongoDB"] = new[]
            {
                "Master MongoDB NoSQL Database Fundamentals",
                "Design Efficient Document Schemas & Collections",
                "Implement MongoDB Aggregation Pipelines",
                "Master MongoDB Performance Optimization",
                "Build MongoDB Replica Sets & Sharding",
                "Implement MongoDB Security & Administration"
            },

            // Cloud Platforms
            ["Azure"] = new[]
            {
                "Master Microsoft Azure Cloud Fundamentals",
                "Implement Azure Compute & Storage Services",
                "Build Azure DevOps CI/CD Pipelines",
                "Deploy Applications to Azure App Service",
                "Master Azure Security & Identity Management",
                "Implement Azure Monitoring & Cost Optimization"
            },
            ["Google Cloud"] = new[]
            {
                "Master Google Cloud Platform Fundamentals",
                "Implement GCP Compute Engine & Storage Solutions",
                "Build GCP Networking & Load Balancing",
                "Deploy Applications to Google Cloud Run",
                "Master GCP Security & Identity Access Management",
                "Implement GCP Monitoring & Cost Management"
            },

            // Emerging Technologies
            ["Blockchain"] = new[]
            {
                "Master Blockchain Technology Fundamentals",
                "Learn Smart Contract Development & Deployment",
                "Implement Blockchain Security Best Practices",
                "Build Decentralized Applications (DApps)",
                "Master Cryptocurrency & Token Economics",
                "Implement Blockchain Integration in Applications"
            },

            // Game Development
            ["Unity"] = new[]
            {
                "Master Unity Game Engine Fundamentals",
                "Build 2D Games with Unity & C#",
                "Develop 3D Games & Interactive Experiences",
                "Implement Unity Game Optimization & Performance",
                "Master Unity Animation & Physics Systems",
                "Publish Unity Games to Multiple Platforms"
            },
            ["Godot"] = new[]
            {
                "Master Godot Game Engine Fundamentals",
                "Build 2D Games with Godot & GDScript",
                "Develop 3D Games & Interactive Applications",
                "Master Godot Scripting & Node System",
                "Implement Godot Game Optimization Techniques",
                "Deploy Godot Games to Multiple Platforms"
            },
            ["Game Development"] = new[]
            {
                "Master Game Development Fundamentals & Design",
                "Implement Game Physics & Mechanics Systems",
                "Build Game AI & Procedural Generation",
                "Master Game Optimization & Performance",
                "Learn Game Monetization & Publishing Strategies",
                "Implement Game Testing & Quality Assurance"
            }
        };

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting seeder...");
        
        var dryRun = args.Any(a => a.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));
        var connectionOverride = GetArgValue(args, "--connection");
        var subjectFilter = GetArgValue(args, "--subject");

        Console.WriteLine($"Dry run: {dryRun}");
        Console.WriteLine($"Subject filter: {subjectFilter ?? "None"}");

        var config = BuildConfiguration();
        var connectionString = string.IsNullOrWhiteSpace(connectionOverride)
            ? config.GetConnectionString("MyCnn")
            : connectionOverride;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine("Missing connection string. Set it in appsettings.json (MyCnn) or pass --connection.");
            return;
        }

        Console.WriteLine($"Using connection string: {connectionString.Substring(0, Math.Min(50, connectionString.Length))}...");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        Console.WriteLine("Creating database context...");
        await using var db = new AppDbContext(options);
        
        Console.WriteLine("Testing database connection...");
        try
        {
            await db.Database.CanConnectAsync();
            Console.WriteLine("Database connection successful!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database connection failed: {ex.Message}");
            return;
        }

        Console.WriteLine("Fetching subjects from database...");
        var subjects = await db.Subjects
            .Where(s => !s.IsDeleted)
            .ToListAsync();

        Console.WriteLine($"Found {subjects.Count} subjects in database");

        var subjectsByName = subjects
            .ToDictionary(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase);

        Console.WriteLine("Fetching existing system goals...");
        var systemGoals = await db.Goals
            .Where(g => g.IsSystemDefined && !g.IsDeleted)
            .ToListAsync();

        Console.WriteLine($"Found {systemGoals.Count} existing system goals");

        var goalsByTitle = new Dictionary<string, Goals>(StringComparer.OrdinalIgnoreCase);
        foreach (var goal in systemGoals)
        {
            var key = goal.Title.Trim();
            if (!goalsByTitle.ContainsKey(key))
            {
                goalsByTitle[key] = goal;
            }
            else
            {
                Console.WriteLine($"Warning: Duplicate goal title found in database: '{key}' - using first occurrence");
            }
        }

        Console.WriteLine("Fetching existing subject-goal links...");
        var existingLinks = await db.SubjectGoals
            .Select(sg => new { sg.SubjectId, sg.GoalId })
            .ToListAsync();

        Console.WriteLine($"Found {existingLinks.Count} existing subject-goal links");

        var existingLinksSet = existingLinks
            .Select(link => $"{link.SubjectId}_{link.GoalId}")
            .ToHashSet();

        var createdGoals = 0;
        var createdLinks = 0;
        var missingSubjects = new List<string>();

        Console.WriteLine("Processing subject-specific goals...");
        // Process subject-specific goals
        foreach (var (subjectName, goalTitles) in SubjectGoalsMap)
        {
            Console.WriteLine($"Processing subject: {subjectName}");
            
            if (!string.IsNullOrWhiteSpace(subjectFilter) &&
                !subjectName.Equals(subjectFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!subjectsByName.TryGetValue(subjectName, out var subject))
            {
                missingSubjects.Add(subjectName);
                Console.WriteLine($"  Subject '{subjectName}' not found in database");
                continue;
            }

            Console.WriteLine($"  Processing {goalTitles.Length} goals for {subjectName}");

            foreach (var title in goalTitles)
            {
                if (!goalsByTitle.TryGetValue(title, out var goal))
                {
                    goal = new Goals
                    {
                        GoalId = Guid.NewGuid(),
                        Title = title,
                        Description = $"System goal: {title}",
                        IsSystemDefined = true,
                        IsActive = true,
                        Duration = GoalDuration.OneMonth,
                        CreatedAt = DateTime.UtcNow
                    };

                    if (!dryRun)
                    {
                        db.Goals.Add(goal);
                    }

                    goalsByTitle[title] = goal;
                    createdGoals++;
                }

                var linkKey = $"{subject.SubjectId}_{goal.GoalId}";
                if (!existingLinksSet.Contains(linkKey))
                {
                    if (!dryRun)
                    {
                        db.SubjectGoals.Add(new SubjectGoal
                        {
                            SubjectId = subject.SubjectId,
                            GoalId = goal.GoalId
                        });
                    }

                    existingLinksSet.Add(linkKey);
                    createdLinks++;
                }
            }
        }

        Console.WriteLine("Processing cross-cutting goals...");
        // Process cross-cutting goals (goals that apply to multiple subjects)
        foreach (var (goalTitle, subjectNames) in CrossCuttingGoals)
        {
            Console.WriteLine($"Processing cross-cutting goal: {goalTitle}");
            
            // Create or get the goal
            if (!goalsByTitle.TryGetValue(goalTitle, out var goal))
            {
                goal = new Goals
                {
                    GoalId = Guid.NewGuid(),
                    Title = goalTitle,
                    Description = $"Cross-cutting system goal: {goalTitle}",
                    IsSystemDefined = true,
                    IsActive = true,
                    Duration = GoalDuration.TwoMonths, // Cross-cutting goals might need more time
                    CreatedAt = DateTime.UtcNow
                };

                if (!dryRun)
                {
                    db.Goals.Add(goal);
                }

                goalsByTitle[goalTitle] = goal;
                createdGoals++;
            }

            // Link this goal to all applicable subjects
            foreach (var subjectName in subjectNames)
            {
                if (!string.IsNullOrWhiteSpace(subjectFilter) &&
                    !subjectName.Equals(subjectFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!subjectsByName.TryGetValue(subjectName, out var subject))
                {
                    if (!missingSubjects.Contains(subjectName))
                    {
                        missingSubjects.Add(subjectName);
                    }
                    continue;
                }

                var linkKey = $"{subject.SubjectId}_{goal.GoalId}";
                if (!existingLinksSet.Contains(linkKey))
                {
                    if (!dryRun)
                    {
                        db.SubjectGoals.Add(new SubjectGoal
                        {
                            SubjectId = subject.SubjectId,
                            GoalId = goal.GoalId
                        });
                    }

                    existingLinksSet.Add(linkKey);
                    createdLinks++;
                }
            }
        }

        Console.WriteLine("Saving changes to database...");
        if (!dryRun)
        {
            await db.SaveChangesAsync();
        }
        Console.WriteLine("Save completed!");

        Console.WriteLine($"Seed complete. Created goals: {createdGoals}, Created links: {createdLinks}");
        Console.WriteLine($"Cross-cutting goals: {CrossCuttingGoals.Count}, Subject-specific goals: {SubjectGoalsMap.SelectMany(x => x.Value).Distinct().Count()}");
        if (missingSubjects.Count > 0)
        {
            Console.WriteLine("Missing subjects (skipped): " + string.Join(", ", missingSubjects));
        }
    }

    private static IConfiguration BuildConfiguration()
    {
        var cwd = Directory.GetCurrentDirectory();
        var apiPath = Path.Combine(cwd, "src", "CodeNexus.API");
        if (!Directory.Exists(apiPath))
        {
            // Fallback to relative path from bin folder
            apiPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CodeNexus.API"));
        }

        return new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string? GetArgValue(string[] args, string key)
    {
        var index = Array.FindIndex(args, a => a.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length)
        {
            return null;
        }

        return args[index + 1];
    }
}
