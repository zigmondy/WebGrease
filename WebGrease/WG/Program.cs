﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft">
//   Copyright Microsoft Corporation, all rights reserved
// </copyright>
// <summary>
//   The entry point of executable.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebGrease
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using Activities;
    using Configuration;
    using Css;
    using Extensions;

    /// <summary>The entry point of the CLI executable.</summary>
    internal sealed class Program
    {
        /// <summary>
        /// flag for determining if an activity has been set already.
        /// </summary>
        private static bool _isActivityAlreadySet = false;

        /// <summary>The main entry point to the tool.</summary>
        /// <param name="args">The command line parameters.</param>
        /// <returns>The program result.</returns>
        internal static int Main(string[] args)
        {
            try
            {
                if (args == null || !args.Any())
                {
                    Console.WriteLine(ResourceStrings.Usage);
                    return 1;
                }

                WebGreaseConfiguration config;
                string configType;
                ActivityMode mode = GenerateConfiguration(args, out config, out configType);

                switch (mode)
                {
                    case ActivityMode.Minify:
                        ExecuteMinification(config, configType);
                        break;
                    case ActivityMode.Validate:
                        ExecuteValidation(config, configType);
                        break;
                    case ActivityMode.Bundle:
                        ExecuteBundling(config, configType);
                        break;
                    case ActivityMode.AutoName:
                        ExecuteHashFiles(config, configType);
                        break;
                    case ActivityMode.SpriteImages:
                        ExecuteImageSpriting(config, configType);
                        break;
                    default:
                        Console.WriteLine(ResourceStrings.Usage);
                        return 1;
                }
            }
            catch (Exception ex)
            {
                // general catch so unhandled exceptions don't result in a stack dump on the screen
                HandleError(ex, null, ResourceStrings.GeneralErrorMessage);
                return -1;
            }
            return 0;
        }

        /// <summary>
        /// Processes css files for images to merge (sprite)
        /// </summary>
        /// <param name="config"></param>
        /// <param name="configType"></param>
        private static void ExecuteImageSpriting(WebGreaseConfiguration config, string configType)
        {
            // whiles this uses the minification activity, it is only assembling images
            var spriter = new MinifyCssActivity
            {
                ShouldMinify = false,
                ShouldOptimize = false,
                ShouldAssembleBackgroundImages = true
            };


            foreach (var fileSet in config.CssFileSets.Where(file => file.InputSpecs.Any()))
            {
                // for each file set, get the configuration and setup the assembler object.
                var spriteConfig = WebGreaseConfiguration.GetNamedConfig(fileSet.ImageSpriting, configType);

                if (spriteConfig.ShouldAutoSprite)
                {
                    var outputPath = GetOutputFolder(fileSet.Output, config.DestinationDirectory);
                    var directoryName = string.IsNullOrWhiteSpace(Path.GetExtension(outputPath)) ? outputPath : Path.GetDirectoryName(outputPath);

                    spriter.ShouldAssembleBackgroundImages = spriteConfig.ShouldAutoSprite;
                    spriter.ImageAssemblyPadding = spriteConfig.ImagePadding.ToString(CultureInfo.InvariantCulture);
                    spriter.ImageAssembleReferencesToIgnore.Clear();
                    foreach (var image in spriteConfig.ImagesToIgnore)
                    {
                        spriter.ImageAssembleReferencesToIgnore.Add(image);
                    }

                    // run the spriter on every file in each of the input specs
                    foreach (var source in fileSet.InputSpecs.SelectMany(inputSpec => GetFiles(inputSpec.Path, inputSpec.SearchPattern, inputSpec.SearchOption)))
                    {
                        spriter.SourceFile = source;

                        spriter.DestinationFile = Path.Combine(directoryName, Path.GetFileName(source));
                        spriter.ImagesOutputDirectory = Path.IsPathRooted(spriteConfig.DestinationImageFolder)
                                                            ? spriteConfig.DestinationImageFolder
                                                            : Path.Combine(directoryName, spriteConfig.DestinationImageFolder);

                        var file = Path.GetFileNameWithoutExtension(spriter.DestinationFile);
                        var scanFilePath = Path.Combine(directoryName, Path.GetFileNameWithoutExtension(file) + ".scan." + Strings.Css);
                        var updateFilePath = Path.Combine(directoryName, Path.GetFileNameWithoutExtension(file) + ".update." + Strings.Css);
                        spriter.ImageAssembleScanDestinationFile = scanFilePath;
                        spriter.ImageAssembleUpdateDestinationFile = updateFilePath;
                        try
                        {
                            spriter.Execute();
                        }
                        catch (WorkflowException ex)
                        {
                            HandleError(ex.InnerException ?? ex, source);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Renames the input files into unique hash values based on their contents.
        /// </summary>
        /// <param name="config"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Generalized catch/log/display pattern"),
        System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "This isn't terribly complex.")]
        private static void ExecuteHashFiles(WebGreaseConfiguration config, string configType)
        {
            var hasher = new FileHasherActivity
            {
                CreateExtraDirectoryLevelFromHashes = true,
                ShouldPreserveSourceDirectoryStructure = false
            };

            // images
            if (config.ImageDirectories.Any())
            {
                hasher.LogFileName = Path.Combine(config.LogsDirectory, Strings.ImagesLogFile);
                hasher.DestinationDirectory = GetOutputFolder(null, config.DestinationDirectory);
                foreach (var imageDirectory in config.ImageDirectories.Distinct())
                {
                    hasher.SourceDirectories.Add(imageDirectory);
                }

                if (config.ImageExtensions != null && config.ImageExtensions.Any())
                {
                    hasher.FileTypeFilter = string.Join(new string(Strings.FileFilterSeparator), config.ImageExtensions.ToArray());
                }

                hasher.Execute();
                hasher.SourceDirectories.Clear();
            }

            // css
            if (config.CssFileSets.Any(file => file.InputSpecs.Any() && !file.Output.IsNullOrWhitespace()))
            {
                hasher.LogFileName = Path.Combine(config.LogsDirectory, Strings.CssLogFile);
                foreach (var fileSet in config.CssFileSets.Where(file => file.InputSpecs.Any() && !file.Output.IsNullOrWhitespace()))
                {
                    var cssConfig = WebGreaseConfiguration.GetNamedConfig(fileSet.Autonaming, configType);

                    if (cssConfig.ShouldAutoName)
                    {
                        var outputPath = GetOutputFolder(fileSet.Output, config.DestinationDirectory);
                        hasher.DestinationDirectory = string.IsNullOrWhiteSpace(Path.GetExtension(outputPath)) ? outputPath : Path.GetDirectoryName(outputPath);
                        foreach (var inputFolder in
                            fileSet.InputSpecs
                                   .SelectMany(inputSpec => GetFiles(inputSpec.Path, inputSpec.SearchPattern, inputSpec.SearchOption))
                                   .Select(Path.GetDirectoryName).Distinct())
                        {
                            // add the distinct folders
                            hasher.SourceDirectories.Add(inputFolder);
                        }

                        hasher.FileTypeFilter = Strings.CssFilter;

                        try
                        {
                            hasher.Execute();
                        }
                        catch (Exception ex)
                        {
                            HandleError(ex);
                        }

                        hasher.SourceDirectories.Clear();
                    }
                }
            }

            // js
            if (config.JSFileSets.Any(file => file.InputSpecs.Any() && !file.Output.IsNullOrWhitespace()))
            {
                hasher.LogFileName = Path.Combine(config.LogsDirectory, Strings.JsLogFile);
                foreach (var fileSet in config.JSFileSets.Where(file => file.InputSpecs.Any() && !file.Output.IsNullOrWhitespace()))
                {
                    var jsConfig = WebGreaseConfiguration.GetNamedConfig(fileSet.Autonaming, configType);

                    if (jsConfig.ShouldAutoName)
                    {
                        var outputPath = GetOutputFolder(fileSet.Output, config.DestinationDirectory);
                        hasher.DestinationDirectory = string.IsNullOrWhiteSpace(Path.GetExtension(outputPath)) ? outputPath : Path.GetDirectoryName(outputPath);
                        hasher.CreateExtraDirectoryLevelFromHashes = true;
                        foreach (var inputFolder in
                            fileSet.InputSpecs
                                   .SelectMany(inputSpec => GetFiles(inputSpec.Path, inputSpec.SearchPattern, inputSpec.SearchOption))
                                   .Select(Path.GetDirectoryName).Distinct())
                        {
                            // add the distinct folders
                            hasher.SourceDirectories.Add(inputFolder);
                        }
                        hasher.FileTypeFilter = Strings.JsFilter;
                        try
                        {
                            hasher.Execute();
                        }
                        catch (Exception ex)
                        {
                            HandleError(ex);
                        }
                        hasher.SourceDirectories.Clear();
                    }
                }
            }
        }

        /// <summary>
        /// Executes a bundling operation on the configuration data
        /// </summary>
        /// <param name="config"></param>
        private static void ExecuteBundling(WebGreaseConfiguration config, string configType)
        {
            var assembler = new AssemblerActivity();

            foreach (var fileSet in config.CssFileSets.Where(file => file.InputSpecs.Any() && !file.Output.IsNullOrWhitespace()))
            {
                var jsConfig = WebGreaseConfiguration.GetNamedConfig(fileSet.Bundling, configType);

                if (jsConfig.ShouldBundleFiles)
                {
                    // for each file set (that isn't empty of inputs)
                    // bundle the files, however this can only be done on filesets that have an output value of a file (ie: has an extension)
                    var outputfile = GetOutputFolder(fileSet.Output, config.DestinationDirectory);

                    if (Path.GetExtension(outputfile).IsNullOrWhitespace())
                    {
                        Console.WriteLine(ResourceStrings.InvalidBundlingOutputFile, outputfile);
                        continue;
                    }

                    assembler.OutputFile = outputfile;
                    assembler.Inputs.Clear();

                    foreach (var inputSpec in fileSet.InputSpecs)
                    {
                        assembler.Inputs.Add(inputSpec);
                    }

                    try
                    {
                        assembler.Execute();
                    }
                    catch (Exception ex)
                    {
                        HandleError(ex);
                    }
                }
            }

            foreach (var fileSet in config.JSFileSets.Where(file => file.InputSpecs.Any() && !file.Output.IsNullOrWhitespace()))
            {
                var cssConfig = WebGreaseConfiguration.GetNamedConfig(fileSet.Bundling, configType);

                if (cssConfig.ShouldBundleFiles)
                {
                    // for each file set (that isn't empty of inputs)
                    // bundle the files, however this can only be done on filesets that have an output value of a file (ie: has an extension)
                    var outputfile = GetOutputFolder(fileSet.Output, config.DestinationDirectory);

                    if (Path.GetExtension(outputfile).IsNullOrWhitespace())
                    {
                        Console.WriteLine(ResourceStrings.InvalidBundlingOutputFile, outputfile);
                        continue;
                    }

                    assembler.OutputFile = outputfile;
                    assembler.Inputs.Clear();
                    foreach (var inputSpec in fileSet.InputSpecs)
                    {
                        assembler.Inputs.Add(inputSpec);
                    }

                    try
                    {
                        assembler.Execute();
                    }
                    catch (Exception ex)
                    {
                        HandleError(ex);
                    }
                }
            }
        }

        /// <summary>
        /// Executes the Validation/Analysis task on javascript files
        /// </summary>
        /// <param name="config"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private static void ExecuteValidation(WebGreaseConfiguration config, string configType)
        {
            var jsValidator = new MinifyJSActivity();

            foreach (var fileSet in config.JSFileSets.Where(set => set.InputSpecs.Any()))
            {
                var jsConfig = WebGreaseConfiguration.GetNamedConfig(fileSet.Validation, configType);

                if (jsConfig.ShouldAnalyze)
                {
                    foreach (var file in fileSet.InputSpecs.SelectMany(inputSpec => GetFiles(inputSpec.Path, inputSpec.SearchPattern, inputSpec.SearchOption)))
                    {
                        // execute analysis on each of the files in the set
                        jsValidator.ShouldMinify = false; // hard set to false since we are only validating in this phase.
                        jsValidator.ShouldAnalyze = jsConfig.ShouldAnalyze;
                        jsValidator.AnalyzeArgs = jsConfig.AnalyzeArguments;
                        jsValidator.SourceFile = file;
                        var outputPath = GetOutputFolder(fileSet.Output, config.DestinationDirectory);
                        jsValidator.DestinationFile = GetOutputFilename(file, outputPath);

                        try
                        {
                            jsValidator.Execute();
                        }
                        catch (Exception ex)
                        {
                            if (ex.InnerException != null && ex.InnerException is BuildWorkflowException)
                            {
                                HandleError(ex.InnerException, file);
                            }
                            else
                            {
                                HandleError(ex, file);
                            }
                        }
                    }
                }
            }

        }

        /// <summary>
        /// Executes the minification task
        /// </summary>
        /// <param name="config">config settings to be used. Only used for the input/output settings.</param>
        private static void ExecuteMinification(WebGreaseConfiguration config, string configType)
        {
            var jsCruncher = new MinifyJSActivity();
            var cssCruncher = new MinifyCssActivity();

            // only run the crunchers if the configuration has files for that particular file type
            foreach (var fileSet in config.JSFileSets.Where(set => set.InputSpecs.Any()))
            {
                ProcessJsFileSet(jsCruncher, fileSet, config, configType);
            }

            // do the same thing for CSS files... nested loops are fun!
            foreach (var fileSet in config.CssFileSets.Where(set => set.InputSpecs.Any()))
            {
                ProcessCssFileSet(cssCruncher, fileSet, config, configType);
            }
        }

        /// <summary>
        /// Process an individual JavaScript file set
        /// </summary>
        /// <param name="jsCruncher">minify js activity</param>
        /// <param name="fileSet">js file set</param>
        /// <param name="config">webgrease configuration</param>
        /// <param name="configType">config type</param>
        private static void ProcessJsFileSet(MinifyJSActivity jsCruncher, JSFileSet fileSet, WebGreaseConfiguration config, string configType)
        {
            var jsConfig = WebGreaseConfiguration.GetNamedConfig(fileSet.Minification, configType);

            if (jsConfig.ShouldMinify)
            {
                foreach (var file in fileSet.InputSpecs.SelectMany(inputSpec => GetFiles(inputSpec.Path, inputSpec.SearchPattern, inputSpec.SearchOption)))
                {
                    // execute minification on each file in the set.

                    // configure settings.
                    jsCruncher.ShouldMinify = jsConfig.ShouldMinify;

                    // if we specified some globals to ignore, format them on the command line with the
                    // other minification arguments
                    if (!string.IsNullOrWhiteSpace(jsConfig.GlobalsToIgnore))
                    {
                        jsCruncher.MinifyArgs = Strings.GlobalsToIgnoreArg + jsConfig.GlobalsToIgnore + ' ' + jsConfig.MinificationArugments;
                    }
                    else
                    {
                        jsCruncher.MinifyArgs = jsConfig.MinificationArugments;
                    }

                    jsCruncher.ShouldAnalyze = false; // we are minimizing, not validating

                    jsCruncher.SourceFile = file;
                    var outputPath = GetOutputFolder(fileSet.Output, config.DestinationDirectory);
                    jsCruncher.DestinationFile = GetOutputFilename(file, outputPath, true);

                    try
                    {
                        // execute
                        jsCruncher.Execute();
                    }
                    catch (Exception ex)
                    {
                        HandleError(ex, file);
                    }
                }
            }
        }

        /// <summary>
        /// Process individual CSS file set
        /// </summary>
        /// <param name="cssCruncher">minify css activity</param>
        /// <param name="fileSet">css file set</param>
        /// <param name="config">webgrease configuration</param>
        /// <param name="configType">config type</param>
        private static void ProcessCssFileSet(MinifyCssActivity cssCruncher, CssFileSet fileSet, WebGreaseConfiguration config, string configType)
        {
            var cssConfig = WebGreaseConfiguration.GetNamedConfig(fileSet.Minification, configType);

            if (cssConfig.ShouldMinify)
            {
                foreach (var file in fileSet.InputSpecs.SelectMany(inputSpec => GetFiles(inputSpec.Path, inputSpec.SearchPattern, inputSpec.SearchOption)))
                {
                    cssCruncher.ShouldMinify = cssConfig.ShouldMinify;
                    cssCruncher.ShouldOptimize = cssConfig.ShouldMinify;

                    foreach (string hack in cssConfig.ForbiddenSelectors)
                    {
                        cssCruncher.HackSelectors.Add(hack);
                    }

                    cssCruncher.SourceFile = file;
                    cssCruncher.ShouldExcludeProperties = true;

                    // disable lower case validation (this is on by default).
                    cssCruncher.ShouldValidateForLowerCase = cssConfig.ShouldValidateLowerCase;

                    // we are just minifying. Image assembly is a different action.
                    cssCruncher.ShouldAssembleBackgroundImages = false;
                    var outputPath = GetOutputFolder(fileSet.Output, config.DestinationDirectory);
                    cssCruncher.DestinationFile = GetOutputFilename(file, outputPath, true);

                    try
                    {
                        cssCruncher.Execute();
                    }
                    catch (Exception ex)
                    {
                        AggregateException aggEx;

                        if (ex.InnerException != null &&
                            (aggEx = ex.InnerException as AggregateException) != null)
                        {
                            // antlr can throw a blob of errors, so they need to be deduped to get the real set of errors
                            IEnumerable<string> messages = ErrorHelper.DedupeCSSErrors(aggEx);
                            DisplayErrors(messages, file);
                        }
                        else
                        {
                            // Catch, record and display error
                            HandleError(ex, file);
                        }
                    }
                }
            }
        }

        /// <summary>Generates the configuration object from command line parameters.</summary>
        /// <param name="args">The command line args.</param>
        /// <param name="wgConfig">The web grease configuration root.</param>
        /// <param name="configType">name of the config sections to use.</param>
        /// <example>
        /// WebGrease.exe /?
        /// WebGrease.exe -in:foo.js out:bar.js
        /// WebGrease.exe [-m|-w] -config:foo.webgrease.config -in:c:\content -out:c:\content\bin\release
        /// </example>
        /// <returns>The configuration object.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        internal static ActivityMode GenerateConfiguration(IEnumerable<string> args, out WebGreaseConfiguration wgConfig, out string configType)
        {
            // reset activity flag since this is a new run
            _isActivityAlreadySet = false;

            Contract.Requires(args != null);

            string configFileName = null;
            string inputPath = Environment.CurrentDirectory;
            string outputPath = Environment.CurrentDirectory;
            string logPath = Environment.CurrentDirectory;
            string tokenPath = null;
            string imagePath = null;
            configType = string.Empty;

            ActivityMode activityToRun = ActivityMode.ShowHelp;

            // process the arguments into variables
            foreach (string arg in args)
            {
                // int value of char '-'
                if (arg[0] == '-')
                {
                    // split the arg into a key/value pair using a colon as the delimeter.
                    int split = arg.IndexOf(':');
                    string key = arg.Substring(1, (split > 0 ? split : arg.Length) - 1);
                    string value = split > -1 ? arg.Substring(split + 1) : string.Empty;

                    switch (key.ToUpperInvariant())
                    {
                        case "C":
                            // config file .. trumps all CLI parameters when used.
                            configFileName = value;
                            break;
                        case "M":
                            // minification of files
                            activityToRun = TrySetActivity(ActivityMode.Minify);
                            break;
                        case "V":
                            // validation of files
                            activityToRun = TrySetActivity(ActivityMode.Validate);
                            break;
                        case "S":
                            // Spriting of images
                            activityToRun = TrySetActivity(ActivityMode.SpriteImages);
                            break;
                        case "A":
                            // auto naming of files
                            activityToRun = TrySetActivity(ActivityMode.AutoName);
                            break;
                        case "B":
                            // Bundle (merge) files
                            activityToRun = TrySetActivity(ActivityMode.Bundle);
                            break;
                        case "IN":
                            // input file/folder
                            inputPath = value;
                            break;
                        case "OUT":
                            // output file/folder                            
                            outputPath = value;
                            break;
                        case "IMAGES":
                            // images folder
                            imagePath = value;
                            break;
                        case "LOG":
                            logPath = value;
                            break;
                        case "TYPE":
                            configType = value;
                            break;
                        default:
                            // show usage help
                            activityToRun = ActivityMode.ShowHelp;
                            break;
                    }
                }
            }

            if (activityToRun == ActivityMode.ShowHelp)
            {
                // something wasn't right with the parameter inputs. return false and a null config.
                wgConfig = null;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(configFileName))
                {
                    try
                    {
                        // use the config file as a base, and set overrides based on CLI input
                        wgConfig = new WebGreaseConfiguration(configFileName, inputPath, outputPath, logPath);
                    }
                    catch (Exception ex)
                    {
                        HandleError(ex, null, ResourceStrings.ConfigurationFileParseError);
                        wgConfig = new WebGreaseConfiguration();
                        return ActivityMode.ShowHelp;
                    }
                }
                else
                {
                    // manual build up of configuration data
                    wgConfig = new WebGreaseConfiguration();

                    // initialize these to the current directory. Manual CLI paramters (not config files), will have their relative paths
                    // computed into the generated input specs.
                    wgConfig.SourceDirectory = Environment.CurrentDirectory;
                    wgConfig.DestinationDirectory = Environment.CurrentDirectory;

                    wgConfig = CreateInputSpecs(wgConfig, inputPath, outputPath);
                }

                wgConfig = OverrideConfig(wgConfig, logPath, tokenPath, imagePath);
            }

            return activityToRun;
        }

        /// <summary>
        /// Checks to see if an activity has already been set before in this run. If so, it sets the activity to ShowHelp
        /// and outputs an error to the user
        /// </summary>
        /// <param name="activityMode">Requested mode to run.</param>
        /// <returns>Either the requested mode or the ShowHelp mode, if an activity was already set.</returns>
        private static ActivityMode TrySetActivity(ActivityMode activityMode)
        {
            if (_isActivityAlreadySet)
            {
                DisplayErrors(new[] { ResourceStrings.MultipleSwitches });
                return ActivityMode.ShowHelp;
            }
            _isActivityAlreadySet = true;
            return activityMode;
        }

        /// <summary>
        /// Creates the input spec objects based on input and output paths.
        /// </summary>
        /// <param name="wgConfig">Config object to use.</param>
        /// <param name="inputPath">Input path from the command parameters</param>
        /// <param name="outputPath">output path from the command parameters</param>
        /// <returns>the updated configuration object.</returns>
        private static WebGreaseConfiguration CreateInputSpecs(WebGreaseConfiguration wgConfig, string inputPath, string outputPath)
        {
            if (inputPath.IsNullOrWhitespace() && outputPath.IsNullOrWhitespace())
            {
                // no paths need to be overriden.
                return wgConfig;
            }

            string outputPathExtension = Path.GetExtension(outputPath);
            string inputPathExtension = Path.GetExtension(inputPath);

            bool createCssInput = false;
            bool createJsInput = false;
            // Set the file filter to the extension of the output path (if it's a file)
            if (!outputPathExtension.IsNullOrWhitespace())
            {
                // if the output path is a file we only process css OR js files into it.
                if (outputPathExtension.EndsWith(Strings.Css, StringComparison.OrdinalIgnoreCase))
                {
                    createCssInput = true;
                }
                else
                {
                    createJsInput = true;
                }
            }
            else if (!inputPathExtension.IsNullOrWhitespace())
            {
                // if the input path is not a folder, only set one of the file sets for processing
                if (inputPathExtension.EndsWith(Strings.Css, StringComparison.OrdinalIgnoreCase))
                {
                    createCssInput = true;
                }
                else
                {
                    createJsInput = true;
                }
            }
            else
            {
                // if both the intput and  output path are not a file, assume they are a folder process both JS and CSS files found within.
                createCssInput = true;
                createJsInput = true;
            }

            var cssFileSet = new CssFileSet();
            var jsFileSet = new JSFileSet();

            // set or update input specs
            if (createCssInput)
            {

                cssFileSet.InputSpecs.Add(GetInputSpec(inputPath, Strings.CssFilter));
            }

            if (createJsInput)
            {
                if (jsFileSet.InputSpecs.Any())
                {
                    jsFileSet.InputSpecs.Clear();
                }

                jsFileSet.InputSpecs.Add(GetInputSpec(inputPath, Strings.JsFilter));

            }

            // set output spec
            jsFileSet.Output = outputPath;
            cssFileSet.Output = outputPath;

            wgConfig.JSFileSets.Add(jsFileSet);
            wgConfig.CssFileSets.Add(cssFileSet);

            return wgConfig;
        }

        /// <summary>
        /// Overrides the given configuration object withe
        /// </summary>
        /// <param name="wgConfig">config file to override</param>
        /// <param name="logPath">path of the log files</param>
        /// <param name="tokenPath">token file path</param>
        /// <param name="imagePath">image file path</param>
        /// <returns>an overriden config object</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase")]
        private static WebGreaseConfiguration OverrideConfig(WebGreaseConfiguration wgConfig, string logPath, string tokenPath, string imagePath)
        {
            // images path
            if (!imagePath.IsNullOrWhitespace())
            {
                // clear out existing config values as cmd line values override.
                wgConfig.ImageDirectories.Clear();

                wgConfig.ImageDirectories.Add(imagePath);
                wgConfig.ImageExtensions = Strings.DefaultImageExtensions;
            }

            // token path
            if (!tokenPath.IsNullOrWhitespace())
            {
                // TODO: should CLI allow specifying override dir as well?
                wgConfig.TokensDirectory = tokenPath;
            }

            wgConfig.LogsDirectory = logPath;

            return wgConfig;
        }

        /// <summary>
        /// returns an input spec instance
        /// </summary>
        /// <param name="inputPath">path for input</param>
        /// <param name="filter">file filter</param>
        /// <returns>an input spec</returns>
        private static InputSpec GetInputSpec(string inputPath, string filter)
        {
            // sanitize the inputpath incase it contains wild cards in it
            // as the filter will be applied to the directory name.
            if (inputPath.Contains('*'))
            {
                inputPath = Path.GetDirectoryName(inputPath);
            }

            return new InputSpec { Path = inputPath.IsNullOrWhitespace() ? Environment.CurrentDirectory : inputPath, SearchPattern = filter, SearchOption = System.IO.SearchOption.TopDirectoryOnly };
        }


        /// <summary>
        /// Gets the collection of files that match the path and filter.
        /// </summary>
        /// <param name="inputPath">Input path to search. Can be a filename, which will be the only member of the returned set.</param>
        /// <param name="searchPattern">Pattern to match for results.</param>
        /// <param name="searchOption">Directory processing option</param>
        /// <returns>A collection of matching files.</returns>
        private static IEnumerable<string> GetFiles(string inputPath, string searchPattern, SearchOption searchOption)
        {
            if (!Path.GetExtension(inputPath).IsNullOrWhitespace())
            {
                // path is a file
                return new[] { inputPath };
            }

            // path is a folder
            return Directory.EnumerateFiles(inputPath, searchPattern, searchOption);
        }

        /// <summary>
        /// Get the output file name for a given input filename and output path.
        /// </summary>
        /// <param name="inputFileName">input filename</param>
        /// <param name="outputPath">Path to be output to.</param>
        /// <param name="useMin">inserts "min" into the file name to dedupe it from the non minified file.</param>
        /// <returns>A new filename.</returns>
        internal static string GetOutputFilename(string inputFileName, string outputPath, bool useMin = false)
        {
            if (outputPath.IsNullOrWhitespace())
            {
                // output path is empty.
                outputPath = Path.GetDirectoryName(inputFileName);
                useMin = true;
            }
            else if (!Path.GetExtension(outputPath).IsNullOrWhitespace())
            {
                // output path is a file so return it.
                return outputPath;
            }

            // output is a folder
            var inputfile = Path.GetFileNameWithoutExtension(inputFileName);
            var ext = Path.GetExtension(inputFileName);

            var outputFile = Path.Combine(outputPath, inputfile + (useMin ? ".min" : string.Empty) + ext ?? string.Empty);

            return outputFile;
        }

        /// <summary>
        /// general handler for errors
        /// </summary>
        /// <param name="ex">exception caught</param>
        /// <param name="file">File being processed that caused the error.</param>
        /// <param name="message">message to be shown (instead of Exception.Message)</param>
        private static void HandleError(Exception ex, string file = null, string message = null)
        {
            DisplayErrors(!string.IsNullOrWhiteSpace(message) ? new[] { message } : new[] { ex.Message }, file);

            if (ex.InnerException != null)
            {
                DisplayErrors(new[] { ex.InnerException.Message });
            }
        }

        /// <summary>
        /// Displays the errors on the console.
        /// </summary>
        /// <param name="messages">error messages to be shown</param>
        /// <param name="file">File being processed that caused the errors.</param>
        private static void DisplayErrors(IEnumerable<string> messages, string file = null)
        {
            var currentColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            if (!string.IsNullOrWhiteSpace(file))
            {
                Console.WriteLine(ResourceStrings.ErrorsInFileFormat, file);
            }
            foreach (var error in messages)
            {
                Console.WriteLine(error);
            }
            Console.ForegroundColor = currentColor;
        }

        /// <summary>
        /// Gets the output folder based on whether the filespec path is rooted or not.
        /// </summary>
        /// <param name="fileSpecPath"></param>
        /// <param name="wgBaseOutputPath"></param>
        /// <returns></returns>
        private static string GetOutputFolder(string fileSpecPath, string wgBaseOutputPath)
        {
            string results;

            // null protection when this paramter is null yet the spec path is relative.
            wgBaseOutputPath = string.IsNullOrWhiteSpace(wgBaseOutputPath) ? Environment.CurrentDirectory : wgBaseOutputPath;

            if (string.IsNullOrWhiteSpace(fileSpecPath))
            {
                results = wgBaseOutputPath;
            }
            else if (Path.IsPathRooted(fileSpecPath))
            {
                results = fileSpecPath;
            }
            else
            {
                // not rooted or null, so it must be relative
                results = Path.Combine(wgBaseOutputPath, fileSpecPath);
            }

            return Path.GetFullPath(results);
        }
    }
}