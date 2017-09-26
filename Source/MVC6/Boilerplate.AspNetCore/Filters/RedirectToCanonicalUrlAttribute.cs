namespace Boilerplate.AspNetCore.Filters
{
    using System;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// To improve Search Engine Optimization SEO, there should only be a single URL for each resource. Case
    /// differences and/or URL's with/without trailing slashes are treated as different URL's by search engines. This
    /// filter redirects all non-canonical URL's based on the settings specified to their canonical equivalent.
    /// Note: Non-canonical URL's are not generated by this site template, it is usually external sites which are
    /// linking to your site but have changed the URL case or added/removed trailing slashes.
    /// (See Google's comments at http://googlewebmastercentral.blogspot.co.uk/2010/04/to-slash-or-not-to-slash.html
    /// and Bing's at http://blogs.bing.com/webmaster/2012/01/26/moving-content-think-301-not-relcanonical).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class RedirectToCanonicalUrlAttribute : Attribute, IResourceFilter
    {
        private const char SlashCharacter = '/';

        private readonly bool appendTrailingSlash;
        private readonly bool lowercaseUrls;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedirectToCanonicalUrlAttribute"/> class.
        /// </summary>
        /// <param name="options">The route options.</param>
        public RedirectToCanonicalUrlAttribute(IOptions<RouteOptions> options)
            : this(options.Value.AppendTrailingSlash, options.Value.LowercaseUrls)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RedirectToCanonicalUrlAttribute" /> class.
        /// </summary>
        /// <param name="appendTrailingSlash">If set to <c>true</c> append trailing slashes, otherwise strip trailing
        /// slashes.</param>
        /// <param name="lowercaseUrls">If set to <c>true</c> lower-case all URL's.</param>
        public RedirectToCanonicalUrlAttribute(
            bool appendTrailingSlash,
            bool lowercaseUrls)
        {
            this.appendTrailingSlash = appendTrailingSlash;
            this.lowercaseUrls = lowercaseUrls;
        }

        /// <summary>
        /// Gets a value indicating whether to append trailing slashes.
        /// </summary>
        /// <value>
        /// <c>true</c> if appending trailing slashes; otherwise, strip trailing slashes.
        /// </value>
        public bool AppendTrailingSlash => this.appendTrailingSlash;

        /// <summary>
        /// Gets a value indicating whether to lower-case all URL's.
        /// </summary>
        /// <value>
        /// <c>true</c> if lower-casing URL's; otherwise, <c>false</c>.
        /// </value>
        public bool LowercaseUrls => this.lowercaseUrls;

        /// <summary>
        /// Executes the resource filter. Called before execution of the remainder of the pipeline. Determines whether
        /// the HTTP request contains a non-canonical URL using <see cref="TryGetCanonicalUrl"/>, if it doesn't calls
        /// the <see cref="HandleNonCanonicalRequest"/> method.
        /// </summary>
        /// <param name="context">The <see cref="T:Microsoft.AspNetCore.Mvc.Filters.ResourceExecutingContext" />.</param>
        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            if (HttpMethods.IsGet(context.HttpContext.Request.Method))
            {
                if (!this.TryGetCanonicalUrl(context, out string canonicalUrl))
                {
                    this.HandleNonCanonicalRequest(context, canonicalUrl);
                }
            }
        }

        /// <summary>
        /// Executes the resource filter. Called after execution of the remainder of the pipeline.
        /// </summary>
        /// <param name="context">The <see cref="T:Microsoft.AspNetCore.Mvc.Filters.ResourceExecutedContext" />.</param>
        public void OnResourceExecuted(ResourceExecutedContext context)
        {
        }

        /// <summary>
        /// Determines whether the specified URl is canonical and if it is not, outputs the canonical URL.
        /// </summary>
        /// <param name="context">The <see cref="T:Microsoft.AspNetCore.Mvc.Filters.ResourceExecutingContext" />.</param>
        /// <param name="canonicalUrl">The canonical URL.</param>
        /// <returns><c>true</c> if the URL is canonical, otherwise <c>false</c>.</returns>
        protected virtual bool TryGetCanonicalUrl(ResourceExecutingContext context, out string canonicalUrl)
        {
            var isCanonical = true;

            var request = context.HttpContext.Request;

            // If we are not dealing with the home page. Note, the home page is a special case and it doesn't matter
            // if there is a trailing slash or not. Both will be treated as the same by search engines.
            if (request.Path.HasValue && (request.Path.Value.Length > 1))
            {
                var hasTrailingSlash = request.Path.Value[request.Path.Value.Length - 1] == SlashCharacter;

                if (this.appendTrailingSlash)
                {
                    // Append a trailing slash to the end of the URL.
                    if (!hasTrailingSlash && !this.HasAttribute<NoTrailingSlashAttribute>(context))
                    {
                        request.Path = new PathString(request.Path.Value + SlashCharacter);
                        isCanonical = false;
                    }
                }
                else
                {
                    // Trim a trailing slash from the end of the URL.
                    if (hasTrailingSlash)
                    {
                        request.Path = new PathString(request.Path.Value.TrimEnd(SlashCharacter));
                        isCanonical = false;
                    }
                }

                if (this.lowercaseUrls && !this.HasAttribute<NoTrailingSlashAttribute>(context))
                {
                    foreach (var character in request.Path.Value)
                    {
                        if (char.IsUpper(character))
                        {
                            request.Path = new PathString(request.Path.Value.ToLower());
                            isCanonical = false;
                            break;
                        }
                    }

                    if (request.QueryString.HasValue && !this.HasAttribute<NoLowercaseQueryStringAttribute>(context))
                    {
                        foreach (var character in request.QueryString.Value)
                        {
                            if (char.IsUpper(character))
                            {
                                request.QueryString = new QueryString(request.QueryString.Value.ToLower());
                                isCanonical = false;
                                break;
                            }
                        }
                    }
                }
            }

            if (isCanonical)
            {
                canonicalUrl = null;
            }
            else
            {
                canonicalUrl = UriHelper.GetEncodedUrl(request);
            }

            return isCanonical;
        }

        /// <summary>
        /// Handles HTTP requests for URL's that are not canonical. Performs a 301 Permanent Redirect to the canonical URL.
        /// </summary>
        /// <param name="context">The <see cref="T:Microsoft.AspNetCore.Mvc.Filters.ResourceExecutingContext" />.</param>
        /// <param name="canonicalUrl">The canonical URL.</param>
        protected virtual void HandleNonCanonicalRequest(ResourceExecutingContext context, string canonicalUrl) =>
            context.Result = new RedirectResult(canonicalUrl, true);

        /// <summary>
        /// Determines whether the specified action or its controller has the attribute with the specified type
        /// <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the attribute.</typeparam>
        /// <param name="context">The <see cref="T:Microsoft.AspNetCore.Mvc.Filters.ResourceExecutingContext" />.</param>
        /// <returns><c>true</c> if a <typeparamref name="T"/> attribute is specified, otherwise <c>false</c>.</returns>
        protected virtual bool HasAttribute<T>(ResourceExecutingContext context)
        {
            foreach (var filterMetadata in context.Filters)
            {
                if (filterMetadata is T)
                {
                    return true;
                }
            }

            return false;
        }
    }
}