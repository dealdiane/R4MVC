﻿using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Moq;
using R4Mvc.Tools;
using R4Mvc.Tools.Services;
using Xunit;

namespace R4Mvc.Test
{
    public class R4MvcGeneratorTests
    {
        private IFilePersistService DummyPersistService => new Mock<IFilePersistService>().Object;
        private R4MvcGeneratorService GetGeneratorService(
            IControllerRewriterService controllerRewriter = null,
            IControllerGeneratorService controllerGenerator = null,
            IStaticFileGeneratorService staticFileGenerator = null,
            IFilePersistService filePersistService = null,
            IViewLocatorService viewLocator = null,
            Settings settings = null)
            => new R4MvcGeneratorService(controllerRewriter, controllerGenerator, staticFileGenerator, filePersistService ?? DummyPersistService, viewLocator, settings ?? new Settings());

        [Fact]
        public void ViewControllers()
        {
            var controllers = new[]
            {
                new ControllerDefinition { Name = "Shared" },                           // Root view only controller
                new ControllerDefinition { Name = "Shared", Area = "Admin" },           // Area view only controller
                new ControllerDefinition { Name = "Shared", Namespace = "Project" },    // Regular controller (should be ignored here)
            };
            var settings = new Settings();
            var service = GetGeneratorService(settings: settings);

            var viewControllers = service.CreateViewOnlyControllerClasses(controllers).ToList();
            Assert.Collection(viewControllers,
                c => Assert.Equal("SharedController", c.Identifier.Value),
                c => Assert.Equal("AdminArea_SharedController", c.Identifier.Value));
            Assert.Collection(controllers,
                c => Assert.StartsWith(settings.R4MvcNamespace, c.FullyQualifiedGeneratedName),
                c => Assert.StartsWith(settings.R4MvcNamespace, c.FullyQualifiedGeneratedName),
                c => Assert.StartsWith("Project", c.FullyQualifiedGeneratedName)); // Don't update this field for regular controllers in this method
        }

        [Fact]
        public void ViewControllers_Sort()
        {
            var controllers = new[]
            {
                new ControllerDefinition { Name = "Shared", Area = "Admin" },
                new ControllerDefinition { Name = "Shared2" },
                new ControllerDefinition { Name = "Shared1" },
            };
            var settings = new Settings();
            var service = GetGeneratorService(settings: settings);

            var viewControllers = service.CreateViewOnlyControllerClasses(controllers).ToList();
            Assert.Collection(viewControllers,
                c => Assert.Equal("Shared1Controller", c.Identifier.Value),
                c => Assert.Equal("Shared2Controller", c.Identifier.Value),
                c => Assert.Equal("AdminArea_SharedController", c.Identifier.Value));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Project.R4")]
        [InlineData("R4MvcCustom")]
        public void ViewControllers_UseSettingsNamespace(string r4Namespace)
        {
            var controllers = new[]
            {
                new ControllerDefinition { Name = "Shared" },
            };
            var settings = new Settings();
            if (r4Namespace != null)
                settings.R4MvcNamespace = r4Namespace;
            var service = GetGeneratorService(settings: settings);

            var viewControllers = service.CreateViewOnlyControllerClasses(controllers).ToList();
            Assert.Collection(controllers, c => Assert.StartsWith(settings.R4MvcNamespace, c.FullyQualifiedGeneratedName));
        }

        [Fact]
        public void AreaClasses()
        {
            var controllers = new[]
            {
                new ControllerDefinition { Name = "Users", Area = "Admin" },
                new ControllerDefinition { Name = "Shared", Area = "Admin" },
                new ControllerDefinition { Name = "Shared" },
            };
            var areaControllers = controllers.ToLookup(c => c.Area);
            var service = GetGeneratorService();

            var areaClasses = service.CreateAreaClasses(areaControllers).ToList();
            Assert.Collection(areaClasses,
                a => Assert.Equal("AdminAreaClass", a.Identifier.Value));
        }

        [Fact]
        public void AreaClasses_Sort()
        {
            var controllers = new[]
            {
                new ControllerDefinition { Name = "Shared", Area = "Admin2" },
                new ControllerDefinition { Name = "Shared", Area = "Admin1" },
            };
            var areaControllers = controllers.ToLookup(c => c.Area);
            var service = GetGeneratorService();

            var areaClasses = service.CreateAreaClasses(areaControllers).ToList();
            Assert.Collection(areaClasses,
                a => Assert.Equal("Admin1AreaClass", a.Identifier.Value),
                a => Assert.Equal("Admin2AreaClass", a.Identifier.Value));
        }

        [Fact]
        public void ActionResultClass()
        {
            var service = GetGeneratorService();
            var actionClass = service.ActionResultClass()
                .AssertIs(SyntaxKind.InternalKeyword, SyntaxKind.PartialKeyword)
                .AssertName(Constants.ActionResultClass);
            Assert.Collection(actionClass.BaseList.Types,
                t => Assert.Equal("ActionResult", (t.Type as IdentifierNameSyntax).Identifier.Value),
                t => Assert.Equal("IR4MvcActionResult", (t.Type as IdentifierNameSyntax).Identifier.Value));
            Assert.Contains(actionClass.Members,
                m =>
                {
                    var constructor = Assert.IsType<ConstructorDeclarationSyntax>(m).AssertIsPublic();
                    Assert.Equal(4, constructor.ParameterList.Parameters.Count);
                    return true;
                });
        }

        [Fact]
        public void JsonResultClass()
        {
            var service = GetGeneratorService();
            var actionClass = service.JsonResultClass()
                .AssertIs(SyntaxKind.InternalKeyword, SyntaxKind.PartialKeyword)
                .AssertName(Constants.JsonResultClass);
            Assert.Collection(actionClass.BaseList.Types,
                t => Assert.Equal("JsonResult", (t.Type as IdentifierNameSyntax).Identifier.Value),
                t => Assert.Equal("IR4MvcActionResult", (t.Type as IdentifierNameSyntax).Identifier.Value));
            Assert.Contains(actionClass.Members,
                m =>
                {
                    var constructor = Assert.IsType<ConstructorDeclarationSyntax>(m).AssertIsPublic();
                    Assert.Equal(4, constructor.ParameterList.Parameters.Count);
                    return true;
                });
        }
    }
}
