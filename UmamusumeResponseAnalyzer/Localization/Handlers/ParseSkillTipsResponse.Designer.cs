﻿//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

namespace UmamusumeResponseAnalyzer.Localization.Handlers {
    using System;
    
    
    /// <summary>
    ///   一个强类型的资源类，用于查找本地化的字符串等。
    /// </summary>
    // 此类是由 StronglyTypedResourceBuilder
    // 类通过类似于 ResGen 或 Visual Studio 的工具自动生成的。
    // 若要添加或移除成员，请编辑 .ResX 文件，然后重新运行 ResGen
    // (以 /str 作为命令选项)，或重新生成 VS 项目。
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class ParseSkillTipsResponse {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal ParseSkillTipsResponse() {
        }
        
        /// <summary>
        ///   返回此类使用的缓存的 ResourceManager 实例。
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("UmamusumeResponseAnalyzer.Localization.Handlers.ParseSkillTipsResponse", typeof(ParseSkillTipsResponse).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   重写当前线程的 CurrentUICulture 属性，对
        ///   使用此强类型资源类的所有资源查找执行重写。
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
        ///   查找类似 [aqua]Average cost-effectiveness: {0}[/] 的本地化字符串。
        /// </summary>
        internal static string I18N_AverageCostEffectiveness {
            get {
                return ResourceManager.GetString("I18N_AverageCostEffectiveness", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 Predicted total score: {0} (learned skills) + {1} (upcoming skills) + {2} (attributes) = {3} ({4}) 的本地化字符串。
        /// </summary>
        internal static string I18N_Caption {
            get {
                return ResourceManager.GetString("I18N_Caption", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 Evaluation points 的本地化字符串。
        /// </summary>
        internal static string I18N_Columns_Grade {
            get {
                return ResourceManager.GetString("I18N_Columns_Grade", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 Required skill points 的本地化字符串。
        /// </summary>
        internal static string I18N_Columns_RequireSP {
            get {
                return ResourceManager.GetString("I18N_Columns_RequireSP", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 Skill name 的本地化字符串。
        /// </summary>
        internal static string I18N_Columns_SkillName {
            get {
                return ResourceManager.GetString("I18N_Columns_SkillName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 Evolution 的本地化字符串。
        /// </summary>
        internal static string I18N_Evolved {
            get {
                return ResourceManager.GetString("I18N_Evolved", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 Due to some skills not meeting the evolution conditions, this result is based on &lt;maximized recommendation when some skills cannot evolve&gt; for recalculation 的本地化字符串。
        /// </summary>
        internal static string I18N_EvolveSkillAlert_1 {
            get {
                return ResourceManager.GetString("I18N_EvolveSkillAlert_1", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 And after recalculation, the total score [green]exceeds[/] the result when unable to evolve, please ensure to learn skills as recommended after both talent skills meet the evolution conditions 的本地化字符串。
        /// </summary>
        internal static string I18N_EvolveSkillAlert_2 {
            get {
                return ResourceManager.GetString("I18N_EvolveSkillAlert_2", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 If you see this prompt, open the option of &quot;save communication packets for debugging&quot; then re-enter the upbringing interface 的本地化字符串。
        /// </summary>
        internal static string I18N_EvolveSkillAlert_3 {
            get {
                return ResourceManager.GetString("I18N_EvolveSkillAlert_3", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 And package all files in the %LOCALAPPDATA%/UmamusumeResponseAnalyzer/packets directory and send them to the channel/github issue for debugging 的本地化字符串。
        /// </summary>
        internal static string I18N_EvolveSkillAlert_4 {
            get {
                return ResourceManager.GetString("I18N_EvolveSkillAlert_4", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 Due to some skills not meeting the evolution conditions, this result is based on &lt;maximized recommendation when some skills cannot evolve&gt; for recalculation 的本地化字符串。
        /// </summary>
        internal static string I18N_EvolveSkillAlert_5 {
            get {
                return ResourceManager.GetString("I18N_EvolveSkillAlert_5", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 And after recalculation, the total score [red]is below[/] the result when unable to evolve, therefore, recommendations are given as (all/part) skills cannot evolve 的本地化字符串。
        /// </summary>
        internal static string I18N_EvolveSkillAlert_6 {
            get {
                return ResourceManager.GetString("I18N_EvolveSkillAlert_6", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 [aqua]The expected cost-effectiveness of skills at different prices is as follows. If the score of a skill is calculated incorrectly and is low and not listed above (in the above situations), please manually calculate the cost-effectiveness and compare with the table below[/] 的本地化字符串。
        /// </summary>
        internal static string I18N_ExpectedCostEffectiveness {
            get {
                return ResourceManager.GetString("I18N_ExpectedCostEffectiveness", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 [green]Expected cost-effectiveness of {0}pt skill: {1}[/] 的本地化字符串。
        /// </summary>
        internal static string I18N_ExpectedCostEffectivenessByPrice {
            get {
                return ResourceManager.GetString("I18N_ExpectedCostEffectivenessByPrice", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 [aqua]Marginal cost-effectiveness: {0}[/] 的本地化字符串。
        /// </summary>
        internal static string I18N_MarginalCostEffectiveness {
            get {
                return ResourceManager.GetString("I18N_MarginalCostEffectiveness", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 [yellow]Known issues [/] 的本地化字符串。
        /// </summary>
        internal static string I18N_ScoreCalculateAttention_1 {
            get {
                return ResourceManager.GetString("I18N_ScoreCalculateAttention_1", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 [yellow]1. For skills that can only be judged whether they can evolve after learning, it may not be correctly judged. If there are evolvable skills that have not evolved above, please judge for yourself [/] 的本地化字符串。
        /// </summary>
        internal static string I18N_ScoreCalculateAttention_2 {
            get {
                return ResourceManager.GetString("I18N_ScoreCalculateAttention_2", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 [yellow]2. Purple (negative) skills not considered, please remove purple skills yourself [/] 的本地化字符串。
        /// </summary>
        internal static string I18N_ScoreCalculateAttention_3 {
            get {
                return ResourceManager.GetString("I18N_ScoreCalculateAttention_3", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 [red]You can decide whether to buy the corresponding skills for the above situations. After buying, restart the game for recalculation [/] 的本地化字符串。
        /// </summary>
        internal static string I18N_ScoreCalculateAttention_4 {
            get {
                return ResourceManager.GetString("I18N_ScoreCalculateAttention_4", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 [red]Below are some reference indicators [/] 的本地化字符串。
        /// </summary>
        internal static string I18N_ScoreCalculateAttention_5 {
            get {
                return ResourceManager.GetString("I18N_ScoreCalculateAttention_5", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 {0} is [yellow]{1}[/] points away 的本地化字符串。
        /// </summary>
        internal static string I18N_ScoreToNextGrade {
            get {
                return ResourceManager.GetString("I18N_ScoreToNextGrade", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 Total skill points: {0}, used skill points: {1}, remaining skill points: {2} 的本地化字符串。
        /// </summary>
        internal static string I18N_Title {
            get {
                return ResourceManager.GetString("I18N_Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 [red]Warning: Unknown purchased skill, id={0}[/] 的本地化字符串。
        /// </summary>
        internal static string I18N_UnknownBoughtSkillAlert {
            get {
                return ResourceManager.GetString("I18N_UnknownBoughtSkillAlert", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 Warning: Unknown skill, group_id={0}, rarity={1} 的本地化字符串。
        /// </summary>
        internal static string I18N_UnknownSkillAlert {
            get {
                return ResourceManager.GetString("I18N_UnknownSkillAlert", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 [red]Warning: There are unknown skills[/] 的本地化字符串。
        /// </summary>
        internal static string I18N_UnknownSkillExistAlert {
            get {
                return ResourceManager.GetString("I18N_UnknownSkillExistAlert", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 , might be the superior skill of {0} 的本地化字符串。
        /// </summary>
        internal static string I18N_UnknownSkillSuperiorSuppose {
            get {
                return ResourceManager.GetString("I18N_UnknownSkillSuperiorSuppose", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 [red]Unknown Uma Musume: {0}, unable to obtain awakening skills, please decide whether to buy yourself.[/] 的本地化字符串。
        /// </summary>
        internal static string I18N_UnknownUma {
            get {
                return ResourceManager.GetString("I18N_UnknownUma", resourceCulture);
            }
        }
    }
}
