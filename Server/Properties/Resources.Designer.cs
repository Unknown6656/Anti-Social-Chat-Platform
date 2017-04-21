﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ASC.Server.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("ASC.Server.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] ASC {
            get {
                object obj = ResourceManager.GetObject("ASC", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;style&gt;
        ///&lt;/style&gt;
        ///&lt;div id=&quot;_chat&quot;&gt;
        ///    User logged in with:
        ///    &lt;br/&gt;
        ///    &lt;pre&gt;§user§&lt;/pre&gt;
        ///    &lt;br /&gt;
        ///    and session:
        ///    &lt;br /&gt;
        ///    &lt;pre&gt;§user_auth§&lt;/pre&gt;
        ///    &lt;br /&gt;
        ///    and location:
        ///    &lt;br /&gt;
        ///    &lt;pre&gt;§location§&lt;/pre&gt;
        ///&lt;/div&gt;.
        /// </summary>
        internal static string chat {
            get {
                return ResourceManager.GetString("chat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;style&gt;
        ///    div#_error, div#_error span {
        ///        font-family: &apos;UnifrakturCook&apos;, cursive;
        ///        align-content: center;
        ///        text-align: center;
        ///        color: #E95E5E;
        ///        width: 100%;
        ///        right: auto;
        ///        left: auto;
        ///    }
        ///
        ///    #_error .big {
        ///        font-size: 2em;
        ///    }
        ///
        ///    #_error .med {
        ///        font-size: 1.5em;
        ///    }
        ///&lt;/style&gt;
        ///&lt;div id=&quot;_error&quot;&gt;    
        ///    &lt;b class=&quot;big&quot;&gt;§error_error§ №§error_code§&lt;/b&gt;&lt;br/&gt;    
        ///    &lt;span class=&quot;med&quot;&gt;[§error_message§]&lt;/span&gt;&lt;br /&gt;
        ///    [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string error {
            get {
                return ResourceManager.GetString("error", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Icon similar to (Icon).
        /// </summary>
        internal static System.Drawing.Icon favicon {
            get {
                object obj = ResourceManager.GetObject("favicon", resourceCulture);
                return ((System.Drawing.Icon)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;!DOCTYPE html&gt;
        ///&lt;!--
        ///    AUTO-GENERATED §time:yyyy-MM-dd HH:mm:ss:ffffff§
        ///    All server variables can be accessed using the identifier between two paragraph-symbols (&apos;§&apos;).
        ///    All resources which are compiled into the assembly must be accessed with &apos;res~____~____&apos; (where the first ____ stands for the resource name and the second ____ for its MIME-type)
        ///--&gt;
        ///&lt;html lang=&quot;§lang_code§&quot; xmlns=&quot;http://www.w3.org/1999/xhtml&quot;&gt;
        ///    &lt;head&gt;
        ///        &lt;meta name=&quot;§lang_name§&quot; content=&quot;§lang_code§&quot;/&gt;
        ///        &lt;met [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string frame {
            get {
                return ResourceManager.GetString("frame", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;!DOCTYPE html&gt;
        ///&lt;!--
        ///    AUTO-GENERATED §time:yyyy-MM-dd HH:mm:ss:ffffff§
        ///    All server variables can be accessed using the identifier between two paragraph-symbols (&apos;§&apos;).
        ///    All resources which are compiled into the assembly must be accessed with &apos;res~____~____&apos; (where the first ____ stands for the resource name and the second ____ for its MIME-type)
        ///--&gt;
        ///&lt;html lang=&quot;§lang_code§&quot; xmlns=&quot;http://www.w3.org/1999/xhtml&quot;&gt;
        ///    &lt;head&gt;
        ///        &lt;meta name=&quot;§lang_name§&quot; content=&quot;§lang_code§&quot;/&gt;
        ///        &lt;met [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string frame_compact {
            get {
                return ResourceManager.GetString("frame_compact", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap ico_de {
            get {
                object obj = ResourceManager.GetObject("ico_de", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap ico_en {
            get {
                object obj = ResourceManager.GetObject("ico_en", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap ico_fr {
            get {
                object obj = ResourceManager.GetObject("ico_fr", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap ico_it {
            get {
                object obj = ResourceManager.GetObject("ico_it", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap ico_ru {
            get {
                object obj = ResourceManager.GetObject("ico_ru", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap ico_zh {
            get {
                object obj = ResourceManager.GetObject("ico_zh", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to /*!
        /// * jQuery Cookie Plugin v1.4.1
        /// * https://github.com/carhartl/jquery-cookie
        /// *
        /// * Copyright 2006, 2014 Klaus Hartl
        /// * Released under the MIT license
        /// */
        ///(function (factory) {
        ///	if (typeof define === &apos;function&apos; &amp;&amp; define.amd) {
        ///		// AMD (Register as an anonymous module)
        ///		define([&apos;jquery&apos;], factory);
        ///	} else if (typeof exports === &apos;object&apos;) {
        ///		// Node/CommonJS
        ///		module.exports = factory(require(&apos;jquery&apos;));
        ///	} else {
        ///		// Browser globals
        ///		factory(jQuery);
        ///	}
        ///}(function ($) {
        ///
        ///	var pluses = /\+/g;
        ///
        ///	funct [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string jquery_cookie {
            get {
                return ResourceManager.GetString("jquery_cookie", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to /*! jQuery v3.1.1 | (c) jQuery Foundation | jquery.org/license */
        ///!function(a,b){&quot;use strict&quot;;&quot;object&quot;==typeof module&amp;&amp;&quot;object&quot;==typeof module.exports?module.exports=a.document?b(a,!0):function(a){if(!a.document)throw new Error(&quot;jQuery requires a window with a document&quot;);return b(a)}:b(a)}(&quot;undefined&quot;!=typeof window?window:this,function(a,b){&quot;use strict&quot;;var c=[],d=a.document,e=Object.getPrototypeOf,f=c.slice,g=c.concat,h=c.push,i=c.indexOf,j={},k=j.toString,l=j.hasOwnProperty,m=l.toString,n=m.call(Object), [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string jquery311min {
            get {
                return ResourceManager.GetString("jquery311min", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;style&gt;
        ///    #_login code {
        ///        /* border: 1px solid #aaa; */
        ///        background-color: #222;
        ///        font-family: monospace;
        ///        display: inline-block;
        ///        font-style: normal;
        ///        color: #bbb;
        ///        padding: 3px;
        ///    }
        ///
        ///    #_guids {
        ///        background-color: #333;
        ///        border: 1px solid #666;
        ///        overflow-x: hidden;
        ///        overflow-y: scroll;
        ///        padding: 6px;
        ///        height: 500px;
        ///        width: calc(100% - 12px);
        ///    }
        ///
        ///    #_guids &gt; div {
        ///        bord [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string login {
            get {
                return ResourceManager.GetString("login", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to /* AUTO-GENERATED §time:yyyy-MM-dd HH:mm:ss:ffffff§ */
        ///
        ///// I cannot use the ES6-backtick syntax, because of incompatiblity with older browsers -__-
        ///
        ///if (!Date.now) {
        ///    Date.now = function now() {
        ///        return new Date().getTime();
        ///    };
        ///}
        ///
        ///$(document).ready(function () {
        ///    $(&apos;#noscript&apos;).css(&apos;display&apos;, &apos;none&apos;);
        ///
        ///    inner = $(&apos;#header, #content, #footer&apos;);
        ///    inner.removeClass(&apos;blurred&apos;);
        ///
        ///    session = $.cookie(&quot;_sess&quot;);
        ///
        ///    $.cookie(&quot;_lang&quot;, &apos;§lang_code§&apos;);
        ///    $(&apos;#content&apos;).cs [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string post_script {
            get {
                return ResourceManager.GetString("post_script", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to /* AUTO-GENERATED §time:yyyy-MM-dd HH:mm:ss:ffffff§ */
        ///
        ///var sha512;
        ///var http_uri = &apos;http://§host§:§port_http§&apos;;
        ///var https_uri = &apos;https://§host§:§port_https§&apos;;
        ///var base_uri = &apos;§protocol§://§host§:§port§/&apos;;
        ///var api_uri = base_uri + &apos;api.json?lang=§lang_code§&apos;;
        ///var is_main_page = §main_page§;
        ///var session = &apos;&apos;;
        ///var avail_lang = [§lang_avail§];
        ///var ival_down;
        ///var inner; // jquery inner
        ///var server_offs; // offset from server time to this time [in ms]
        ///var user = §user§;
        ///.
        /// </summary>
        internal static string pre_script {
            get {
                return ResourceManager.GetString("pre_script", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap profile_default {
            get {
                object obj = ResourceManager.GetObject("profile_default", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to (function () {
        ///    var b;
        ///    if (!(b = t)) {
        ///        var w = Math,
        ///            y = {}, B = y.p = {}, aa = function () { }, C = B.A = {
        ///                extend: function (a) {
        ///                    aa.prototype = this;
        ///                    var c = new aa;
        ///                    a &amp;&amp; c.u(a);
        ///                    c.z = this;
        ///                    return c
        ///                },
        ///                create: function () {
        ///                    var a = this.extend();
        ///                    a.h.apply(a, arguments);
        ///                    r [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string sha512 {
            get {
                return ResourceManager.GetString("sha512", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to /* AUTO-GENERATED §time:yyyy-MM-dd HH:mm:ss:ffffff§ */
        ///
        ///@import url(&apos;https://fonts.googleapis.com/css?family=UnifrakturCook:700&apos;);
        ///
        ///
        ///::-webkit-scrollbar-track {
        ///    -webkit-box-shadow: inset 0 0 6px rgba(0,0,0,0.3);
        ///    background-color: rgba(245, 245, 245, 0.21);
        ///}
        ///
        ///::-webkit-scrollbar {
        ///    /* background-color: #F5F5F5; */
        ///    border-radius: 10px;
        ///    width: 10px;
        ///}
        ///
        ///::-webkit-scrollbar-thumb {
        ///    background-color: #F90;
        ///    background-image: -webkit-linear-gradient(45deg,
        ///             [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string style {
            get {
                return ResourceManager.GetString("style", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to /* DESKTOP ONLY */
        ///.
        /// </summary>
        internal static string style_desktop {
            get {
                return ResourceManager.GetString("style_desktop", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to /* MOBILE ONLY */
        ///.
        /// </summary>
        internal static string style_mobile {
            get {
                return ResourceManager.GetString("style_mobile", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] Unknown6656 {
            get {
                object obj = ResourceManager.GetObject("Unknown6656", resourceCulture);
                return ((byte[])(obj));
            }
        }
    }
}
