﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MinifyCssActivityTest.cs" company="Microsoft">
//   Copyright Microsoft Corporation, all rights reserved
// </copyright>
// <summary>
//   This is a test class for MinifyCssActivityTest and is intended
//   to contain all MinifyCssActivityTest Unit Tests
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebGrease.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using Activities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WebGrease.Tests;
    using WebGrease.Configuration;
    using WebGrease.Extensions;

    /// <summary>This is a test class for MinifyCssActivityTest and is intended
    /// to contain all MinifyCssActivityTest Unit Tests</summary>
    [TestClass]
    public class MinifyCssActivityTest
    {
        /// <summary>The black listed selectors.</summary>
        private static readonly HashSet<string> BlackListedSelectors = new HashSet<string> { "html>body", "* html", "*:first-child+html p", "head:first-child+body", "head+body", "body>", "*>html", "*html>body" };

        /// <summary>
        /// Gets or sets the test context which provides
        /// information about and functionality for the current test run.
        /// </summary>
        public TestContext TestContext { get; set; }

        /// <summary>A test for Css pipeline for property exclusion.</summary>
        [TestMethod]
        [TestCategory(TestCategories.MinifyCssActivity)]
        public void CssExcludePropertiesTest()
        {
            var sourceDirectory = Path.Combine(TestDeploymentPaths.TestDirectory, @"WebGrease.Tests\MinifyCssActivityTest");
            var minifyCssActivity = new MinifyCssActivity(new WebGreaseContext(new WebGreaseConfiguration()));
            minifyCssActivity.SourceFile = Path.Combine(sourceDirectory, @"Input\Case1\ExcludeByKeys.css");
            minifyCssActivity.DestinationFile = Path.Combine(sourceDirectory, @"Output\Case1\ExcludeByKeys.css");
            minifyCssActivity.ShouldExcludeProperties = true;
            minifyCssActivity.ShouldAssembleBackgroundImages = false;
            minifyCssActivity.Execute();

            // Assertions
            var outputFilePath = minifyCssActivity.DestinationFile;
            Assert.IsTrue(File.Exists(outputFilePath));
            var text = File.ReadAllText(outputFilePath);
            Assert.IsTrue(!string.IsNullOrWhiteSpace(text));
            Assert.IsTrue(!text.Contains("Exclude"));
        }

        /// <summary>A test for Css pipeline for lower case validation.</summary>
        [TestMethod]
        [TestCategory(TestCategories.MinifyCssActivity)]
        public void CssLowerCaseValidationTest()
        {
            var sourceDirectory = Path.Combine(TestDeploymentPaths.TestDirectory, @"WebGrease.Tests\MinifyCssActivityTest");
            var minifyCssActivity = new MinifyCssActivity(new WebGreaseContext(new WebGreaseConfiguration()));
            minifyCssActivity.SourceFile = Path.Combine(sourceDirectory, @"Input\Case2\LowerCaseValidation.css");
            minifyCssActivity.DestinationFile = Path.Combine(sourceDirectory, @"Output\Case2\LowerCaseValidation.css");
            minifyCssActivity.ShouldValidateForLowerCase = true;
            minifyCssActivity.ShouldAssembleBackgroundImages = false;
            minifyCssActivity.Execute();

            // Assertions
            var outputFilePath = minifyCssActivity.DestinationFile;
            Assert.IsTrue(File.Exists(outputFilePath));
            var text = File.ReadAllText(outputFilePath);
            Assert.IsTrue(!string.IsNullOrWhiteSpace(text));
        }

        /// <summary>A test for Css pipeline for hack selectors.</summary>
        [TestMethod]
        [TestCategory(TestCategories.MinifyCssActivity)]
        public void CssHackSelectorsTest()
        {
            var sourceDirectory = Path.Combine(TestDeploymentPaths.TestDirectory, @"WebGrease.Tests\MinifyCssActivityTest");
            var minifyCssActivity = new MinifyCssActivity(new WebGreaseContext(new WebGreaseConfiguration()));
            minifyCssActivity.SourceFile = Path.Combine(sourceDirectory, @"Input\Case3\HackValidation.css");
            minifyCssActivity.DestinationFile = Path.Combine(sourceDirectory, @"Output\Case3\HackValidation.css");
            minifyCssActivity.ShouldAssembleBackgroundImages = false;
            foreach (var hack in BlackListedSelectors)
            {
                minifyCssActivity.HackSelectors.Add(hack);
            }

            Exception exception = null;
            try
            {
                minifyCssActivity.Execute();
            }
            catch (AggregateException aggregateException)
            {
                exception = aggregateException.InnerExceptions.FirstOrDefault();
            }
            catch (WorkflowException workflowException)
            {
                exception = workflowException;
            }

            // shouldn't assert, but should log the error and NOT create output file
            Assert.IsFalse(File.Exists(minifyCssActivity.DestinationFile));
        }

        /// <summary>A test for Css pipeline for banned selectors.</summary>
        [TestMethod]
        [TestCategory(TestCategories.MinifyCssActivity)]
        public void CssBannedSelectorsTest()
        {
            var sourceDirectory = Path.Combine(TestDeploymentPaths.TestDirectory, @"WebGrease.Tests\MinifyCssActivityTest");
            var minifyCssActivity = new MinifyCssActivity(new WebGreaseContext(new WebGreaseConfiguration()));
            minifyCssActivity.SourceFile = Path.Combine(sourceDirectory, @"Input\Case4\HackValidation.css");
            minifyCssActivity.DestinationFile = Path.Combine(sourceDirectory, @"Output\Case4\HackValidation.css");
            foreach (var hack in BlackListedSelectors)
            {
                minifyCssActivity.BannedSelectors.Add(hack);
            }

            minifyCssActivity.ShouldAssembleBackgroundImages = false;
            minifyCssActivity.Execute();

            // Assertions
            var outputFilePath = minifyCssActivity.DestinationFile;
            Assert.IsTrue(File.Exists(outputFilePath));
            var text = File.ReadAllText(outputFilePath);
            Assert.IsTrue(string.IsNullOrWhiteSpace(text));
        }

        /// <summary>A test for Css optimization.</summary>
        [TestMethod]
        [TestCategory(TestCategories.MinifyCssActivity)]
        public void CssOptimizationTest()
        {
            var sourceDirectory = Path.Combine(TestDeploymentPaths.TestDirectory, @"WebGrease.Tests\MinifyCssActivityTest");
            var minifyCssActivity = new MinifyCssActivity(new WebGreaseContext(new WebGreaseConfiguration()));
            minifyCssActivity.SourceFile = Path.Combine(sourceDirectory, @"Input\Case5\OptimizationTest.css");
            minifyCssActivity.DestinationFile = Path.Combine(sourceDirectory, @"Output\Case5\OptimizationTest.css");
            minifyCssActivity.ShouldOptimize = true;
            minifyCssActivity.ShouldAssembleBackgroundImages = false;
            minifyCssActivity.Execute();

            // Assertions
            var outputFilePath = minifyCssActivity.DestinationFile;
            Assert.IsTrue(File.Exists(outputFilePath));
            var text = File.ReadAllText(outputFilePath);
            Assert.IsTrue(!string.IsNullOrWhiteSpace(text));
            Assert.IsTrue(!text.Contains("#foo"));
        }

        /// <summary>A test for Css sprite.</summary>
        [TestMethod]
        [TestCategory(TestCategories.MinifyCssActivity)]
        [TestCategory(TestCategories.Spriting)]
        public void CssImageSpriteTest()
        {
            var sourceDirectory = Path.Combine(TestDeploymentPaths.TestDirectory, @"WebGrease.Tests\MinifyCssActivityTest");
            var minifyCssActivity = new MinifyCssActivity(new WebGreaseContext(new WebGreaseConfiguration()));
            minifyCssActivity.SourceFile = Path.Combine(sourceDirectory, @"Input\Case6\SpriteTest.css");
            minifyCssActivity.ImageAssembleScanDestinationFile = Path.Combine(sourceDirectory, @"Output\Case6\SpriteTest_Scan.css");
            minifyCssActivity.ImagesOutputDirectory = Path.Combine(sourceDirectory, @"Output\Case6\Images\");
            minifyCssActivity.DestinationFile = Path.Combine(sourceDirectory, @"Output\Case6\SpriteTest.css");
            minifyCssActivity.ShouldAssembleBackgroundImages = true;
            minifyCssActivity.OutputUnit = "rem";
            minifyCssActivity.OutputUnitFactor = 0.1;
            minifyCssActivity.ShouldMinify = true;
            minifyCssActivity.ShouldOptimize = true;
            minifyCssActivity.Execute();

            // Assertions
            var outputFilePath = minifyCssActivity.DestinationFile;

            // mapping file (so we can look up the target name of the assembled image, as the generated image can be different based on gdi dll versions)
            var mapFilePath = minifyCssActivity.ImageAssembleScanDestinationFile + ".xml";
            var testImage = "media.gif";

            Assert.IsTrue(File.Exists(outputFilePath));
            
            Assert.IsTrue(File.Exists(mapFilePath));
            // verify our test file is in the xml file and get the source folder and assembled file name.
            string relativePath;
            using (var fs = File.OpenRead(mapFilePath))
            {
                var mapFile = XDocument.Load(fs);
                var inputElement = mapFile.Root.Descendants()
                    // get at the input elements
                    .Descendants().Where(e => e.Name == "input")
                    // now at the source file name
                    .Descendants().FirstOrDefault(i => i.Name == "originalfile" && i.Value.Contains(testImage));

                // get the output 
                var outputElement = inputElement.Parent.Parent;

                // get the input path from the location of the css file and the output path where the destination file is.
                var inputPath = Path.GetDirectoryName(inputElement.Value).ToLowerInvariant();
                var outputPath = outputElement.Attribute("file").Value.ToLowerInvariant();

                // diff the paths to get the relative path (as found in the final file)
                relativePath = outputPath.MakeRelativeTo(inputPath);
            }

            var text = File.ReadAllText(outputFilePath);
            Assert.IsTrue(text.Contains("background:0 0 url(" + relativePath + ") no-repeat;"));
        }

        /// <summary>A test for Css sprite.</summary>
        [TestMethod]
        [TestCategory(TestCategories.MinifyCssActivity)]
        [TestCategory(TestCategories.Spriting)]
        public void CssImageSpriteTest2()
        {
            var sourceDirectory = Path.Combine(
                TestDeploymentPaths.TestDirectory, @"WebGrease.Tests\MinifyCssActivityTest");
            var minifyCssActivity = new MinifyCssActivity(new WebGreaseContext(new WebGreaseConfiguration()));
            minifyCssActivity.SourceFile = Path.Combine(sourceDirectory, @"Input\Case7\SpriteTest.css");
            minifyCssActivity.ImageAssembleScanDestinationFile = Path.Combine(
                sourceDirectory, @"Output\Case7\SpriteTest_Scan.css");
            Path.Combine(
                sourceDirectory, @"Output\Case7\SpriteTest_Update.css");
            minifyCssActivity.ImagesOutputDirectory = Path.Combine(sourceDirectory, @"Output\Case6\Images\");
            minifyCssActivity.DestinationFile = Path.Combine(sourceDirectory, @"Output\Case7\SpriteTest.css");
            minifyCssActivity.ShouldAssembleBackgroundImages = true;
            minifyCssActivity.OutputUnit = "rem";
            minifyCssActivity.OutputUnitFactor = 0.1;
            minifyCssActivity.ShouldMinify = true;
            minifyCssActivity.ShouldOptimize = true;
            minifyCssActivity.Execute();

            // Assertions
            var outputFilePath = minifyCssActivity.DestinationFile;

            // mapping file (so we can look up the target name of the assembled image, as the generated image can be different based on gdi dll versions)
            var mapFilePath = minifyCssActivity.ImageAssembleScanDestinationFile + ".xml";
            var testImage = "media.gif";

            Assert.IsTrue(File.Exists(outputFilePath));

            Assert.IsTrue(File.Exists(mapFilePath));
            // verify our test file is in the xml file and get the source folder and assembled file name.
            using (var fs = File.OpenRead(mapFilePath))
            {
                var mapFile = XDocument.Load(fs);
                mapFile.Root.Descendants()
                    // get at the input elements
                       .Descendants().Where(e => e.Name == "input")
                    // now at the source file name
                       .Descendants().FirstOrDefault(i => i.Name == "originalfile" && i.Value.Contains(testImage));
            }

            // Minified result
            outputFilePath = minifyCssActivity.DestinationFile;
            Assert.IsTrue(File.Exists(outputFilePath));
            var text = File.ReadAllText(outputFilePath);
            Assert.IsTrue(!text.Contains("/*"));
            Assert.IsTrue(!text.Contains("*/"));
            Assert.IsTrue(!text.Contains(";;"));
        }

        /// <summary>
        /// Preserve important Comment inside of ruleset
        /// </summary>
        [TestMethod]
        [TestCategory(TestCategories.MinifyCssActivity)]
        public void CSSImportantCommentTest()
        {
            var sourceDirectory = Path.Combine(TestDeploymentPaths.TestDirectory, @"WebGrease.Tests\MinifyCssActivityTest");
            var minifyCssActivity = new MinifyCssActivity(new WebGreaseContext(new WebGreaseConfiguration()));
            minifyCssActivity.SourceFile = Path.Combine(sourceDirectory, @"Input\Case8\comment.css");
            minifyCssActivity.DestinationFile = Path.Combine(sourceDirectory, @"Output\Case8\comment.css");
            minifyCssActivity.ShouldValidateForLowerCase = false;
            minifyCssActivity.ShouldAssembleBackgroundImages = false;
            minifyCssActivity.Execute();

            // Assertions
            var outputFilePath = minifyCssActivity.DestinationFile;
            Assert.IsTrue(File.Exists(outputFilePath));
            var text = File.ReadAllText(outputFilePath);
            Assert.IsTrue(!string.IsNullOrWhiteSpace(text));
            Assert.IsTrue(text.Contains("/*! this is comment inside of ruleset*/"));
            Assert.IsTrue(!text.Contains("/* regular comment */"));
        }

        /// <summary>
        /// Preserve Comment outside of ruleset
        /// </summary>
        [TestMethod]
        [TestCategory(TestCategories.MinifyCssActivity)]
        public void CSSImportantOutsideCommentTest()
        {
            var sourceDirectory = Path.Combine(TestDeploymentPaths.TestDirectory, @"WebGrease.Tests\MinifyCssActivityTest");
            var minifyCssActivity = new MinifyCssActivity(new WebGreaseContext(new WebGreaseConfiguration()));
            minifyCssActivity.SourceFile = Path.Combine(sourceDirectory, @"Input\Case8\commentOutside.css");
            minifyCssActivity.DestinationFile = Path.Combine(sourceDirectory, @"Output\Case8\commentOutside.css");
            minifyCssActivity.ShouldValidateForLowerCase = false;
            minifyCssActivity.ShouldAssembleBackgroundImages = false;
            minifyCssActivity.Execute();

            // Assertions
            var outputFilePath = minifyCssActivity.DestinationFile;
            Assert.IsTrue(File.Exists(outputFilePath));
            var text = File.ReadAllText(outputFilePath);
            Assert.IsTrue(!string.IsNullOrWhiteSpace(text));
            Assert.IsTrue(text.Contains("/*! this is comment outside of ruleset*/"));
            Assert.IsTrue(!text.Contains("/* regular comment */"));
        }

        /// <summary>
        /// Preserve comment before expression
        /// </summary>
        [TestMethod]
        [TestCategory(TestCategories.MinifyCssActivity)]
        public void CSSImportantExpressionCommentTest()
        {
            var sourceDirectory = Path.Combine(TestDeploymentPaths.TestDirectory, @"WebGrease.Tests\MinifyCssActivityTest");
            var minifyCssActivity = new MinifyCssActivity(new WebGreaseContext(new WebGreaseConfiguration()));
            minifyCssActivity.SourceFile = Path.Combine(sourceDirectory, @"Input\Case8\commentExpression.css");
            minifyCssActivity.DestinationFile = Path.Combine(sourceDirectory, @"Output\Case8\commentExpression.css");
            minifyCssActivity.ShouldValidateForLowerCase = false;
            minifyCssActivity.ShouldAssembleBackgroundImages = false;
            minifyCssActivity.Execute();

            // Assertions
            var outputFilePath = minifyCssActivity.DestinationFile;
            Assert.IsTrue(File.Exists(outputFilePath));
            var text = File.ReadAllText(outputFilePath);
            Assert.IsTrue(!string.IsNullOrWhiteSpace(text));
            Assert.IsTrue(text.Contains("/*!expression*/"));
            Assert.IsTrue(!text.Contains("/* regular comment */"));
        }

        /// <summary>
        /// Preserve comment after term
        /// </summary>
        [TestMethod]
        [TestCategory(TestCategories.MinifyCssActivity)]
        public void CSSImportantTermCommentTest()
        {
            var sourceDirectory = Path.Combine(TestDeploymentPaths.TestDirectory, @"WebGrease.Tests\MinifyCssActivityTest");
            var minifyCssActivity = new MinifyCssActivity(new WebGreaseContext(new WebGreaseConfiguration()));
            minifyCssActivity.SourceFile = Path.Combine(sourceDirectory, @"Input\Case8\commentTerm.css");
            minifyCssActivity.DestinationFile = Path.Combine(sourceDirectory, @"Output\Case8\commentTerm.css");
            minifyCssActivity.ShouldValidateForLowerCase = false;
            minifyCssActivity.ShouldAssembleBackgroundImages = false;
            minifyCssActivity.Execute();

            // Assertions
            var outputFilePath = minifyCssActivity.DestinationFile;
            Assert.IsTrue(File.Exists(outputFilePath));
            var text = File.ReadAllText(outputFilePath);
            Assert.IsTrue(!string.IsNullOrWhiteSpace(text));
            Assert.IsTrue(text.Contains("/*! term*/"));
            Assert.IsTrue(!text.Contains("/* regular comment */"));
        }

        /// <summary>
        /// Allow Binary operators inside functions such as calc.
        /// </summary>
        [TestMethod]
        [TestCategory(TestCategories.MinifyCssActivity)]
        public void CSSBinaryOperatorTest()
        {
            var sourceDirectory = Path.Combine(TestDeploymentPaths.TestDirectory, @"WebGrease.Tests\MinifyCssActivityTest");
            var minifyCssActivity = new MinifyCssActivity(new WebGreaseContext(new WebGreaseConfiguration()));
            minifyCssActivity.SourceFile = Path.Combine(sourceDirectory, @"Input\Case9\functionsWithBinaryOperators.css");
            minifyCssActivity.DestinationFile = Path.Combine(sourceDirectory, @"Output\Case9\functionsWithBinaryOperators.css");
            minifyCssActivity.ShouldValidateForLowerCase = false;
            minifyCssActivity.ShouldAssembleBackgroundImages = false;
            minifyCssActivity.Execute();

            // Assertions
            var outputFilePath = minifyCssActivity.DestinationFile;
            Assert.IsTrue(File.Exists(outputFilePath));
            var text = File.ReadAllText(outputFilePath);
            Assert.IsTrue(!string.IsNullOrWhiteSpace(text));
            Assert.IsTrue(text.Contains("calc(100%/3 - 2*1em - 2*1px)"));
            Assert.IsTrue(text.Contains("calc(1em - 2px) calc(1em - 1px)"));
            Assert.IsTrue(text.Contains("min(10% + 20px,300px"));
        }

        /// <summary>
        /// to support multiple url hashing in @font-face css.
        /// </summary>
        [TestMethod]
        [TestCategory(TestCategories.MinifyCssActivity)]
        public void MultipleFontFaceUrlTest()
        {

            var sourceDirectory = Path.Combine(TestDeploymentPaths.TestDirectory, @"WebGrease.Tests\MinifyCssActivityTest");
            var minifyCssActivity = new MinifyCssActivity(new WebGreaseContext(new WebGreaseConfiguration { SourceDirectory = Path.Combine(sourceDirectory, @"Input\Case10") }));
            minifyCssActivity.ImageDirectories = new List<string> { Path.Combine(sourceDirectory, @"Input\Case10\fonts") };
            minifyCssActivity.ImageExtensions = new List<string> { "*.eot", "*.svg", "*.ttf", "*.woff" };
            minifyCssActivity.SourceFile = Path.Combine(sourceDirectory, @"Input\Case10\FontFaceHashing.css");
            minifyCssActivity.DestinationFile = Path.Combine(sourceDirectory, @"Output\Case10\FontFaceHashing.css");
            minifyCssActivity.NonMergeSelectors = new HashSet<string> { "@font-face" };
            minifyCssActivity.ShouldValidateForLowerCase = true;
            minifyCssActivity.ShouldAssembleBackgroundImages = false;
            minifyCssActivity.ShouldOptimize = false;
            minifyCssActivity.ShouldMinify = true;

            var fileHasherActivity = new FileHasherActivity(new WebGreaseContext(new WebGreaseConfiguration()));
            //fileHasherActivity.SourceDirectories.Add(Path.Combine(sourceDirectory, "fonts"));
            var destinationDirectory = Path.Combine(TestDeploymentPaths.TestDirectory, @"Output\Case10\fonts");
            fileHasherActivity.DestinationDirectory = destinationDirectory;
            fileHasherActivity.CreateExtraDirectoryLevelFromHashes = true;
            fileHasherActivity.ShouldPreserveSourceDirectoryStructure = false;
            fileHasherActivity.ConfigType = string.Empty;
            fileHasherActivity.BasePrefixToRemoveFromOutputPathInLog = destinationDirectory;
            fileHasherActivity.LogFileName = Path.Combine(sourceDirectory, @"Output\Case10\css_log.xml");

            minifyCssActivity.Execute(imageHasher: fileHasherActivity);

            // Assertions
            var outputFilePath = minifyCssActivity.DestinationFile;
            Assert.IsTrue(File.Exists(outputFilePath));
            var text = File.ReadAllText(outputFilePath);
            var expectedText = File.ReadAllText(Path.Combine(sourceDirectory, @"Input\Case10\FontFaceHashing-hashed.css"));
            Assert.IsTrue(text.Equals(expectedText, StringComparison.OrdinalIgnoreCase));
        }
        
        [TestMethod]
        [TestCategory(TestCategories.MinifyCssActivity)]
        public void TokenUrlsShouldNotBeHashed()
        {

            var sourceDirectory = Path.Combine(TestDeploymentPaths.TestDirectory, @"WebGrease.Tests\MinifyCssActivityTest");
            var minifyCssActivity = new MinifyCssActivity(new WebGreaseContext(new WebGreaseConfiguration { SourceDirectory = Path.Combine(sourceDirectory, @"Input\Case10") }));
            minifyCssActivity.SourceFile = Path.Combine(sourceDirectory, @"Input\Case11\TokenImageIgnore.css");
            minifyCssActivity.DestinationFile = Path.Combine(sourceDirectory, @"Output\Case11\TokenImageIgnore.css");

            var fileHasherActivity = new FileHasherActivity(new WebGreaseContext(new WebGreaseConfiguration()));

            minifyCssActivity.Execute(imageHasher: fileHasherActivity);

            // Assertions
            var outputFilePath = minifyCssActivity.DestinationFile;
            Assert.IsTrue(File.Exists(outputFilePath));
            var text = File.ReadAllText(outputFilePath);
            Assert.IsTrue(text.Contains("%IMAGE:abcdefg%"));
        }
    }
}
