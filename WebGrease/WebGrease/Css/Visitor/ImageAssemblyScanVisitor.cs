﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageAssemblyScanVisitor.cs" company="Microsoft">
//   Copyright Microsoft Corporation, all rights reserved
// </copyright>
// <summary>
//   Provides the implementation for ImageAssembly log visitor
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebGrease.Css.Visitor
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using Ast;
    using Ast.MediaQuery;
    using Extensions;
    using ImageAssemble;
    using ImageAssemblyAnalysis;
    using ImageAssemblyAnalysis.LogModel;
    using ImageAssemblyAnalysis.PropertyModel;
    using ImageAssembleException = ImageAssemblyAnalysis.ImageAssembleException;

    /// <summary>Provides the implementation for ImageAssembly log visitor</summary>
    public sealed class ImageAssemblyScanVisitor : NodeVisitor
    {
        /// <summary>
        /// The css path
        /// </summary>
        private readonly string _cssPath;

        /// <summary>
        /// The default image asssembly scan output.
        /// </summary>
        private readonly ImageAssemblyScanOutput _defaultImageAssemblyScanOutput = new ImageAssemblyScanOutput();

        /// <summary>
        /// The list of image references populated from AST for background images
        /// declarations which does not meet the criteria of image assembly
        /// </summary>
        private readonly ImageAssemblyAnalysisLog _imageAssemblyAnalysisLog = new ImageAssemblyAnalysisLog();

        /// <summary>
        /// The list of image assembly outputs
        /// </summary>
        private readonly IList<ImageAssemblyScanOutput> _imageAssemblyScanOutputs = new List<ImageAssemblyScanOutput>();

        /// <summary>
        /// The list of image references which should be ignored
        /// while scanning the AST
        /// </summary>
        private readonly HashSet<string> _imageReferencesToIgnore = new HashSet<string>();

        /// <summary>
        /// The list of image references populated from AST for background images
        /// declarations which does not meet the criteria of image assembly
        /// </summary>
        private readonly HashSet<string> _imagesCriteriaFailedReferences = new HashSet<string>();

        /// <summary>Initializes a new instance of the ImageAssemblyScanVisitor class</summary>
        /// <param name="cssPath">The css file path which would be used to configure the image path</param>
        /// <param name="imageReferencesToIgnore">The list of image references to ignore</param>
        /// <param name="additionalImageAssemblyBuckets">The list of additional image references to scan</param>
        public ImageAssemblyScanVisitor(string cssPath, IEnumerable<string> imageReferencesToIgnore, IEnumerable<ImageAssemblyScanInput> additionalImageAssemblyBuckets)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(cssPath));

            // Add the scan outputs
            _imageAssemblyScanOutputs.Add(_defaultImageAssemblyScanOutput);

            // Normalize css path
            _cssPath = cssPath.GetFullPathWithLowercase();

            if (imageReferencesToIgnore != null)
            {
                // Normalize the image references paths to ignore
                imageReferencesToIgnore.ForEach(imageReferenceToIgnore =>
                                                    {
                                                        var path = imageReferenceToIgnore.MakeAbsoluteTo(_cssPath);
                                                        if (!string.IsNullOrWhiteSpace(path))
                                                        {
                                                            _imageReferencesToIgnore.Add(path);
                                                        }
                                                    });
            }

            if (additionalImageAssemblyBuckets == null)
            {
                return;
            }

            // Expand the additional bucket image paths relative to the css
            foreach (var additionalImageSpriteBucket in additionalImageAssemblyBuckets)
            {
                var imagesInBucket = new HashSet<string>();


                // Normalize the image references paths to lazy load
                additionalImageSpriteBucket.ImagesInBucket.ForEach(imagePath =>
                                                                              {
                                                                                  var path = imagePath.MakeAbsoluteTo(_cssPath);
                                                                                  if (!string.IsNullOrWhiteSpace(path))
                                                                                  {
                                                                                      imagesInBucket.Add(path);
                                                                                  }
                                                                              });

                // Add to the member
                _imageAssemblyScanOutputs.Add(new ImageAssemblyScanOutput { ImageAssemblyScanInput = new ImageAssemblyScanInput(additionalImageSpriteBucket.BucketName, imagesInBucket.ToSafeReadOnlyCollection()) });
            }
        }

        /// <summary>
        /// Gets the Default Image Assembly Scan Output.
        /// </summary>
        public ImageAssemblyScanOutput DefaultImageAssemblyScanOutput
        {
            get { return _defaultImageAssemblyScanOutput; }
        }

        /// <summary>
        /// Gets the Image Assembly Scan Outputs.
        /// </summary>
        public IList<ImageAssemblyScanOutput> ImageAssemblyScanOutputs
        {
            get { return _imageAssemblyScanOutputs; }
        }

        /// <summary>Gets the Image Assembly Analysis Log.</summary>
        public ImageAssemblyAnalysisLog ImageAssemblyAnalysisLog
        {
            get { return _imageAssemblyAnalysisLog; }
        }

        /// <summary>The <see cref="StyleSheetNode"/> visit implementation</summary>
        /// <param name="styleSheet">The styleSheet AST node</param>
        /// <returns>The modified AST node if modified otherwise the original node</returns>
        public override AstNode VisitStyleSheetNode(StyleSheetNode styleSheet)
        {
            _imagesCriteriaFailedReferences.Clear();
            styleSheet.StyleSheetRules.ForEach(styleSheetRuleNode => styleSheetRuleNode.Accept(this));
            return styleSheet;
        }

        /// <summary>The <see cref="RulesetNode"/> visit implementation</summary>
        /// <param name="rulesetNode">The ruleset AST node</param>
        /// <returns>The modified AST node if modified otherwise the original node</returns>
        public override AstNode VisitRulesetNode(RulesetNode rulesetNode)
        {
            this.VisitBackgroundDeclarationNode(rulesetNode.Declarations, rulesetNode);
            return rulesetNode;
        }

        /// <summary>The <see cref="MediaNode"/> visit implementation</summary>
        /// <param name="mediaNode">The media AST node</param>
        /// <returns>The modified AST node if modified otherwise the original node</returns>
        public override AstNode VisitMediaNode(MediaNode mediaNode)
        {
            mediaNode.Rulesets.ForEach(rulesetNode => rulesetNode.Accept(this));
            mediaNode.PageNodes.ForEach(pageNode => pageNode.Accept(this));
            return mediaNode;
        }

        /// <summary>The <see cref="PageNode"/> visit implementation</summary>
        /// <param name="pageNode">The page AST node</param>
        /// <returns>The modified AST node if modified otherwise the original node</returns>
        public override AstNode VisitPageNode(PageNode pageNode)
        {
            this.VisitBackgroundDeclarationNode(pageNode.Declarations, pageNode);
            return pageNode;
        }

        /// <summary>The <see cref="TermWithOperatorNode"/> visit implementation</summary>
        /// <param name="termWithOperatorNode">The term AST node</param>
        /// <returns>The modified AST node if modified otherwise the original node</returns>
        public override AstNode VisitTermWithOperatorNode(TermWithOperatorNode termWithOperatorNode)
        {
            termWithOperatorNode.TermNode.Accept(this);

            return termWithOperatorNode;
        }

        /// <summary>Visits the background declaration node
        /// Example Css with shorthand declaration:
        /// #selector
        /// {
        ///   background: url(../../i/02/3118D8F3781159C8341246BBF2B4CA.gif) no-repeat -10px -200px;
        /// }
        /// Example Css with long declarations:
        /// #selector
        /// {
        ///   background-repeat: no-repeat;
        ///   background-position: -10px  -200px;
        ///   background-image: url(../../i/02/3118D8F3781159C8341246BBF2B4CA.gif);
        /// }</summary>
        /// <param name="declarations">The list of declarations</param>
        /// <param name="parent">The parent AST node</param>
        private void VisitBackgroundDeclarationNode(IEnumerable<DeclarationNode> declarations, AstNode parent)
        {
            try
            {
                // There should be 0 or 1 declaration nodes in a set of declarations which
                // should be either "background" or "background-image". Both shorthand and
                // specific declaration are not allowed in a set of declarations.
                Background backgroundNode;
                BackgroundImage backgroundImageNode;
                BackgroundPosition backgroundPositionNode;

                var imagesCriteriaFailedUrls = new List<string>();

                if (!declarations.TryGetBackgroundDeclaration(
                    _cssPath, // For image path/logging etc
                    parent, // For printing the Pretty Print Node for logging
                    out backgroundNode, 
                    out backgroundImageNode, 
                    out backgroundPositionNode, 
                    imagesCriteriaFailedUrls, // Images which don't pass the spriting criteria
                    _imageReferencesToIgnore, // Images which should not be considered for spriting
                    _imageAssemblyAnalysisLog))
                {
                    // Store the list of failed urls
                    imagesCriteriaFailedUrls.ForEach(imagesCriteriaFailedUrl =>
                    {
                        var url = imagesCriteriaFailedUrl.MakeAbsoluteTo(_cssPath);

                        // Throw an exception if image has passed the criteria in past and now
                        // now fails the criteria
                        if (_imageAssemblyScanOutputs.Any(imageAssemblyScanOutput => imageAssemblyScanOutput.ImageReferencesToAssemble.Where(imageReference => imageReference.ImagePath == url).Any()))
                        {
                            throw new ImageAssembleException(string.Format(CultureInfo.CurrentUICulture, CssStrings.DuplicateImageReferenceWithDifferentRulesError, url));
                        }

                        _imagesCriteriaFailedReferences.Add(url);
                    });

                    return;
                }

                if (backgroundNode != null)
                {
                    // Short hand declaration found:
                    // #selector
                    // {
                    // background: url(../../i/02/3118D8F3781159C8341246BBF2B4CA.gif) no-repeat -10px -200px;
                    // }
                    // Add the url and target image position to found list
                    this.AddImageReference(backgroundNode.Url, backgroundNode.BackgroundPosition);
                }
                else if (backgroundImageNode != null && backgroundPositionNode != null)
                {
                    // Long declaration found for background-image:
                    // #selector
                    // {
                    // background-image: url(../../i/02/3118D8F3781159C8341246BBF2B4CA.gif);
                    // background-position: -10px -200px;
                    // }
                    // Add the url and target image position to found list
                    this.AddImageReference(backgroundImageNode.Url, backgroundPositionNode);
                }
            }
            catch (Exception exception)
            {
                throw new ImageAssembleException(string.Format(CultureInfo.CurrentUICulture, CssStrings.InnerExceptionSelector, parent.PrettyPrint()), exception);
            }
        }

        /// <summary>Adds the url in list which would be passed to the
        /// image assembly tool for concatenation</summary>
        /// <param name="url">The url for sprite candidate image</param>
        /// <param name="backgroundPosition">THe background position</param>
        private void AddImageReference(string url, BackgroundPosition backgroundPosition)
        {
            // Add the background image path in the list
            url = url.MakeAbsoluteTo(_cssPath);

            // No need to report the url if it is present in ignore list
            if (_imageReferencesToIgnore.Contains(url))
            {
                return;
            }

            if (_imagesCriteriaFailedReferences.Contains(url))
            {
                // Throw an exception if image has failed the criteria in past and now
                // now passes the criteria
                throw new ImageAssembleException(string.Format(CultureInfo.CurrentUICulture, CssStrings.DuplicateImageReferenceWithDifferentRulesError, url));
            }

            var imagePosition = backgroundPosition.GetImagePositionInVerticalSprite();
            var added = false;

            ////
            //// Try adding the image in the non default scan output
            ////
            for (var count = 1; count < _imageAssemblyScanOutputs.Count; count++)
            {
                var imageAssemblyScanOutput = _imageAssemblyScanOutputs[count];
                if (!imageAssemblyScanOutput.ImageAssemblyScanInput.ImagesInBucket.Contains(url))
                {
                    continue;
                }

                // Make sure that image don't exist already in list
                if (imageAssemblyScanOutput.ImageReferencesToAssemble.Where(inputImage => inputImage.ImagePath == url && inputImage.Position == imagePosition).Count() > 0)
                {
                    continue;
                }

                imageAssemblyScanOutput.ImageReferencesToAssemble.Add(new InputImage { ImagePath = url, Position = imagePosition });
                added = true;
            }

            if (added)
            {
                return;
            }

            ////
            //// Add the image in the default scan output
            ////
            if (_defaultImageAssemblyScanOutput.ImageReferencesToAssemble.Where(inputImage => inputImage.ImagePath == url && inputImage.Position == imagePosition).Any())
            {
                return;
            }

            _defaultImageAssemblyScanOutput.ImageReferencesToAssemble.Add(new InputImage { ImagePath = url, Position = imagePosition });
        }
    }
}