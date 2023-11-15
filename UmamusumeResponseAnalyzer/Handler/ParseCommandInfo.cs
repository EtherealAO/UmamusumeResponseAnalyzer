﻿using Gallop;
using Spectre.Console;
using System.Text.RegularExpressions;
using UmamusumeResponseAnalyzer.Entities;
using UmamusumeResponseAnalyzer.Game;
using UmamusumeResponseAnalyzer.AI;
using MessagePack;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel.Design;
using System.IO.Pipes;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;
using UmamusumeResponseAnalyzer.Communications.Subscriptions;

namespace UmamusumeResponseAnalyzer.Handler
{
    public static partial class Handlers
    {
        public static void ParseCommandInfo(Gallop.SingleModeCheckEventResponse @event)
        {
            if ((@event.data.unchecked_event_array != null && @event.data.unchecked_event_array.Length > 0) || @event.data.race_start_info != null) return;
            SubscribeCommandInfo.Signal(@event);
            var shouldWriteStatistics = true;
            var turnNum = @event.data.chara_info.turn;
            var LArcIsAbroad = (turnNum >= 37 && turnNum <= 43) || (turnNum >= 61 && turnNum <= 67);

            var currentFiveValue = new int[]
            {
                @event.data.chara_info.speed,
                @event.data.chara_info.stamina,
                @event.data.chara_info.power ,
                @event.data.chara_info.guts ,
                @event.data.chara_info.wiz ,
            };
            var fiveValueMaxRevised = new int[]
            {
                ScoreUtils.ReviseOver1200(@event.data.chara_info.max_speed),
                ScoreUtils.ReviseOver1200(@event.data.chara_info.max_stamina),
                ScoreUtils.ReviseOver1200(@event.data.chara_info.max_power) ,
                ScoreUtils.ReviseOver1200(@event.data.chara_info.max_guts) ,
                ScoreUtils.ReviseOver1200(@event.data.chara_info.max_wiz) ,
            };
            var currentFiveValueRevised = currentFiveValue.Select(x => ScoreUtils.ReviseOver1200(x)).ToArray();
            var totalValue = currentFiveValueRevised.Sum();
            AnsiConsole.WriteLine(string.Empty);

            if (GameStats.currentTurn != turnNum - 1 //正常情况
                && GameStats.currentTurn != turnNum //重复显示
                && turnNum != 1 //第一个回合
                )
            {
                GameStats.isFullGame = false;
                AnsiConsole.MarkupLine($"[red]警告：回合数不正确，上一个回合为{GameStats.currentTurn}，当前回合为{turnNum}[/]");
            }
            else if (turnNum == 1)
            {
                GameStats.isFullGame = true;
            }

            if (!@event.IsScenario(ScenarioType.LArc))
                shouldWriteStatistics = false;
            if (GameStats.currentTurn != turnNum - 1)
                shouldWriteStatistics = false;
            if (!GameStats.isFullGame)
                shouldWriteStatistics = false;

            //买技能，大师杯剧本年末比赛，会重复显示
            var isRepeat = @event.data.chara_info.playing_state != 1;
            //if(isRepeat)
            //    AnsiConsole.MarkupLine($"[yellow]******playingstate{@event.data.chara_info.playing_state}******[/]");
            //初始化TurnStats
            if (isRepeat)
            {
                AnsiConsole.MarkupLine($"[yellow]******此回合为重复显示******[/]");
            }
            else
            {
                GameStats.whichScenario = @event.data.chara_info.scenario_id;
                GameStats.currentTurn = turnNum;
                GameStats.stats[turnNum] = new TurnStats();
            }

#if WRITE_GAME_STATISTICS
            if (shouldWriteStatistics)
            {
                GameStats.LArcWriteStatsLastTurn(@event);
            }
#endif
            //为了避免写判断，对于重复回合，直接让turnStat指向一个无用的TurnStats类
            var turnStat = isRepeat ? new TurnStats() : GameStats.stats[turnNum];
            var gameYear = (turnNum - 1) / 24 + 1;
            var gameMonth = ((turnNum - 1) % 24) / 2 + 1;
            var halfMonth = (turnNum % 2 == 0) ? "后半" : "前半";
            var totalTurns = @event.IsScenario(ScenarioType.LArc) ? 67 : 78;

            AnsiConsole.MarkupLine($"[#00ffff]------------------------------------------------------------------------------------[/]");
            AnsiConsole.MarkupLine($"[green]回合数：{@event.data.chara_info.turn}/{totalTurns}, 第{gameYear}年{gameMonth}月{halfMonth}[/]");

            var motivation = @event.data.chara_info.motivation;
            turnStat.motivation = motivation;

            //显示统计信息
            GameStats.print();

            var currentVital = @event.data.chara_info.vital;
            var maxVital = @event.data.chara_info.max_vital;
            if (currentVital < 30)
                AnsiConsole.MarkupLine($"[red]体力：{currentVital}[/]/{maxVital}");
            else if (currentVital < 50)
                AnsiConsole.MarkupLine($"[darkorange]体力：{currentVital}[/]/{maxVital}");
            else if (currentVital < 70)
                AnsiConsole.MarkupLine($"[yellow]体力：{currentVital}[/]/{maxVital}");
            else
                AnsiConsole.MarkupLine($"[green]体力：{currentVital}[/]/{maxVital}");

            if (motivation == 5)
                AnsiConsole.MarkupLine($"干劲[green]绝好调[/]");
            else if (motivation == 4)
                AnsiConsole.MarkupLine($"干劲[yellow]好调[/]");
            else if (motivation == 3)
                AnsiConsole.MarkupLine($"干劲[red]普通[/]");
            else if (motivation == 2)
                AnsiConsole.MarkupLine($"干劲[red]不调[/]");
            else if (motivation == 1)
                AnsiConsole.MarkupLine($"干劲[red]绝不调[/]");

            var totalValueWithPt = totalValue + @event.data.chara_info.skill_point;
            var totalValueWithHalfPt = totalValue + 0.5 * @event.data.chara_info.skill_point;
            AnsiConsole.MarkupLine($"[aqua]总属性：{totalValue}[/]\t[aqua]总属性+0.5*pt：{totalValueWithHalfPt}[/]");

            #region LArc
            //计算训练等级
            if (@event.IsScenario(ScenarioType.LArc))//预测训练等级
            {
                for (int i = 0; i < 5; i++)
                {
                    if (turnNum == 1)
                    {
                        turnStat.trainLevel[i] = 1;
                        turnStat.trainLevelCount[i] = 0;
                    }
                    else
                    {
                        var lastTrainLevel = GameStats.stats[turnNum - 1] != null ? GameStats.stats[turnNum - 1].trainLevel[i] : 1;
                        var lastTrainLevelCount = GameStats.stats[turnNum - 1] != null ? GameStats.stats[turnNum - 1].trainLevelCount[i] : 0;

                        turnStat.trainLevel[i] = lastTrainLevel;
                        turnStat.trainLevelCount[i] = lastTrainLevelCount;
                        if (GameStats.stats[turnNum - 1] != null &&
                            GameStats.stats[turnNum - 1].playerChoice == GameGlobal.TrainIds[i] &&
                            !GameStats.stats[turnNum - 1].isTrainingFailed &&
                            !((turnNum - 1 >= 37 && turnNum - 1 <= 43) || (turnNum - 1 >= 61 && turnNum - 1 <= 67))
                            )//上回合点的这个训练，计数+1
                            turnStat.trainLevelCount[i] += 1;
                        if (turnStat.trainLevelCount[i] >= 4)
                        {
                            turnStat.trainLevelCount[i] -= 4;
                            turnStat.trainLevel[i] += 1;
                        }
                        //检查是否有期待度上升
                        int appRate = @event.data.arc_data_set.arc_info.approval_rate;
                        int oldAppRate = GameStats.stats[turnNum - 1] != null ? (GameStats.stats[turnNum - 1].larc_totalApproval + 85) / 170 : 0;
                        if (oldAppRate < 200 && appRate >= 200)
                            turnStat.trainLevel[i] += 1;
                        if (oldAppRate < 600 && appRate >= 600)
                            turnStat.trainLevel[i] += 1;
                        if (oldAppRate < 1000 && appRate >= 1000)
                            turnStat.trainLevel[i] += 1;

                        if (turnStat.trainLevel[i] >= 5)
                        {
                            turnStat.trainLevel[i] = 5;
                            turnStat.trainLevelCount[i] = 0;
                        }

                    }
                }
            }
            //额外显示LArc信息
            if (@event.IsScenario(ScenarioType.LArc))
            {
                int totalSSLevel = 0;
                int[] rivalBoostCount = new int[4] { 0, 0, 0, 0 };
                int[] effectCount = new int[13] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                foreach (var rival in @event.data.arc_data_set.arc_rival_array)
                {
                    if (rival.selection_peff_array == null)//马娘自身
                        continue;
                    totalSSLevel += rival.star_lv;
                    rivalBoostCount[rival.rival_boost] += 1;

                    foreach (var ef in rival.selection_peff_array)
                    {
                        effectCount[ef.effect_group_id] += 1;
                    }
                }
                //显示所有npc升级奖励的统计
                string toPrint = "";
                foreach (var ef in GameGlobal.LArcSSEffectNameFullColored)
                {
                    toPrint += $"{ef.Value}:[#ff0000]{effectCount[ef.Key]}[/] ";
                }
                //AnsiConsole.MarkupLine(toPrint);//永远固定，所以不用显示
                int totalApproval = @event.data.arc_data_set.arc_rival_array.Sum(x => x.approval_point);
                turnStat.larc_totalApproval = totalApproval;
                int approval_rate = @event.data.arc_data_set.arc_info.approval_rate;
                int shixingPt = @event.data.arc_data_set.arc_info.global_exp;

                int lastTurnTotalApproval = GameStats.stats[turnNum - 1] != null ? GameStats.stats[turnNum - 1].larc_totalApproval : 0;

                int approval_rate_level = approval_rate / 50;
                if (approval_rate_level > 40) { approval_rate_level = 40; }
                int approval_training_bonus = GameGlobal.LArcTrainBonusEvery5Percent[approval_rate_level];

                AnsiConsole.MarkupLine($"期待度：[#00ff00]{approval_rate / 10}.{approval_rate % 10}%[/]（训练[#00ffff]+{approval_training_bonus}%[/]）    适性pt：[#00ff00]{shixingPt}[/]    总支援pt：[#00ff00]{totalApproval}[/]([aqua]+{totalApproval - lastTurnTotalApproval}[/])");

                int totalCount = totalSSLevel * 3 + rivalBoostCount[1] * 1 + rivalBoostCount[2] * 2 + rivalBoostCount[3] * 3;
                AnsiConsole.MarkupLine($"总格数：[#00ff00]{totalCount}[/]    总SS数：[#00ff00]{totalSSLevel}[/]    0123格：[aqua]{rivalBoostCount[0]} {rivalBoostCount[1]} {rivalBoostCount[2]} [/][#00ff00]{rivalBoostCount[3]}[/]");

                toPrint = "";
                //每个人头（包括支援卡）每3级一定有一个属性，一个pt，一个特殊词条。其中特殊词条在一局内是固定的
                //每局15个人头的每种特殊词条的总数是固定的。但是除了几个特殊的（体力最大值-茶座、爱娇-黄金船、练习上手-神鹰），其他都会随机分配给支援卡和路人
                //支援卡相比路人点的次数更多，如果第三回合的支援卡随机分配的特殊词条不好，就可以重开了

                if (turnNum <= 2)
                {
                    //清空SSRivalsSpecialBuffs
                    GameStats.SSRivalsSpecialBuffs = new Dictionary<int, int>();
                }
                else
                {
                    //补全SSRivalsSpecialBuffs，或者检查是否和已存储的SSRivalsSpecialBuffs自洽
                    GameStats.SSRivalsSpecialBuffs ??= new Dictionary<int, int>();

                    GameStats.SSRivalsSpecialBuffs[1014] = 9;
                    GameStats.SSRivalsSpecialBuffs[1007] = 8;

                    foreach (var arc_data in @event.data.arc_data_set.arc_rival_array)
                    {
                        if (arc_data.selection_peff_array == null)//马娘自身
                            continue;

                        if (!GameStats.SSRivalsSpecialBuffs.ContainsKey(arc_data.chara_id))
                            GameStats.SSRivalsSpecialBuffs[arc_data.chara_id] = 0; //未知状态

                        foreach (var ef in arc_data.selection_peff_array)
                        {
                            var efid = ef.effect_group_id;
                            if (efid != 1 && efid != 11) //特殊buff
                            {
                                var efid_old = GameStats.SSRivalsSpecialBuffs[arc_data.chara_id];
                                if (efid_old == 0)
                                    GameStats.SSRivalsSpecialBuffs[arc_data.chara_id] = efid;
                                else if (efid_old != efid)//要么是出错，要么是神鹰的练习上手+适性pt
                                {
                                    if (efid_old == 7 && efid == 9)
                                    {
                                        GameStats.SSRivalsSpecialBuffs[arc_data.chara_id] = 9;
                                    }
                                    else if (efid_old == 9 && efid == 7)
                                    {
                                        //什么都不用做
                                    }
                                    else
                                    {
                                        AnsiConsole.MarkupLine($"[red]警告：larc的ss特殊buff错误，{arc_data.chara_id} {efid} {efid_old}[/]");
                                    }
                                }
                            }
                        }
                    }
                }

                var supportCards1 = @event.data.chara_info.support_card_array.ToDictionary(x => x.position, x => x.support_card_id); //当前S卡卡组
                for (int cardCount = 0; cardCount < 8; cardCount++)
                {
                    if (supportCards1.Any(x => x.Key == cardCount))
                    {

                        var name = Database.Names.GetCharacter(supportCards1[cardCount]).Nickname.EscapeMarkup(); //partner是当前S卡卡组的index（1~6，7是啥？我忘了）或者charaId（10xx)
                        string charaTrainingType = "";
                        string specialBuffs = "";
                        var chara_id = @event.data.arc_data_set.evaluation_info_array.First(x => x.target_id == cardCount).chara_id;
                        if (@event.data.arc_data_set.arc_rival_array.Any(x => x.chara_id == chara_id))
                        {
                            var arc_data = @event.data.arc_data_set.arc_rival_array.First(x => x.chara_id == chara_id);

                            charaTrainingType = $"[red]({GameGlobal.TrainNames[arc_data.command_id]})[/]";

                            if (GameStats.SSRivalsSpecialBuffs[arc_data.chara_id] != 0)
                                specialBuffs = GameGlobal.LArcSSEffectNameFullColored[GameStats.SSRivalsSpecialBuffs[arc_data.chara_id]];
                            else
                                specialBuffs = "?";
                        }
                        toPrint += $"{name}:{charaTrainingType}{specialBuffs} ";
                    }
                }
                AnsiConsole.MarkupLine(toPrint);

                //游戏统计，用于测试游戏里各种概率
                if (@event.data.arc_data_set.selection_info != null && @event.data.arc_data_set.selection_info.is_special_match == 1)//sss对战
                    turnStat.larc_isSSS = true;

                if (@event.data.arc_data_set.selection_info != null && @event.data.arc_data_set.selection_info.params_inc_dec_info_array != null && @event.data.arc_data_set.selection_info.all_win_approval_point != 0)
                {
                    var ssStatus = @event.data.arc_data_set.selection_info.params_inc_dec_info_array;
                    int totalStatusBonus = 0;

                    for (int i = 0; i < 5; i++)
                        totalStatusBonus += ScoreUtils.ReviseOver1200(currentFiveValue[i] + ssStatus.First(x => x.target_type == i + 1).value) - ScoreUtils.ReviseOver1200(currentFiveValue[i]);

                    int headnum = @event.data.arc_data_set.selection_info.selection_rival_info_array.Length;
                    int linkNum = 0;
                    //数一下有几个link卡
                    foreach (var sshead in @event.data.arc_data_set.selection_info.selection_rival_info_array)
                    {
                        if (GameGlobal.LArcScenarioLinkCharas.Any(x => x == sshead.chara_id))
                        {
                            int cardPos = @event.data.arc_data_set.evaluation_info_array.First(x => x.chara_id == sshead.chara_id).target_id;
                            if (cardPos <= 6 && cardPos >= 1)//是支援卡
                                linkNum += 1;
                        }

                    }
                    int headBonus = 4 * headnum + 2 * linkNum;//link卡+6属性，非link卡+4
                    double approvalBonus = approval_rate <= 250 ? 0.0 : (approval_rate - 200) / 500.0;
                    //if (turnNum >= 67) approvalBonus += 0.2;
                    double predictValue = headBonus + headnum * (1.0 + approvalBonus) * 5;
                    if (@event.data.arc_data_set.selection_info.is_special_match == 1)
                        predictValue += 5 * 15;

                    //AnsiConsole.MarkupLine($"SS Match属性（只算下层）：[#00ff00]{totalStatusBonus}[/]，预测[#ff8000]{predictValue:F2}[/]，预测误差[#ff8000]{predictValue - totalStatusBonus:F2}[/]，除以人头[#00ffff]{(double)(totalStatusBonus - headBonus) / headnum:F2}[/]");
                    int selfApprovalPoint = @event.data.arc_data_set.arc_rival_array.First(x => x.chara_id == @event.data.chara_info.card_id / 100).approval_point;

                    int rivalApprovalPoint = 0;

                    foreach (var sshead in @event.data.arc_data_set.selection_info.selection_rival_info_array)
                    {
                        rivalApprovalPoint += @event.data.arc_data_set.arc_rival_array.First(x => x.chara_id == sshead.chara_id).approval_point;
                    }
                    //AnsiConsole.MarkupLine($"自己pt：[#00ff00]{selfApprovalPoint}[/]，其他人pt：[#00ff00]{rivalApprovalPoint}[/]");

#if WRITE_GAME_STATISTICS
                    GameStats.writeGameStatistics("ssValue", $"{totalStatusBonus} {headnum} {linkNum} {approval_rate} {approval_training_bonus} {selfApprovalPoint} {rivalApprovalPoint} {turnNum}");

#endif
                }
                //凯旋门前显示技能性价比
                if (turnNum == 43 || turnNum == 67)
                {
                    var tips = CalculateSkillScoreCost(@event, false);

                    var table1 = new Table();
                    table1.Title("技能性价比排序");
                    table1.AddColumns("技能名称", "pt", "评分", "性价比");
                    table1.Columns[0].Centered();
                    foreach (var i in tips
                        // (string Name, int Cost, int Grade, double Cost-Performance)
                        .Select(tip => (tip.Name, tip.GetRealCost(@event.data.chara_info), tip.GetRealGrade(@event.data.chara_info), (double)tip.GetRealGrade() / tip.GetRealCost()))
                        .OrderByDescending(x => x.Item4))
                    {
                        table1.AddRow($"{i.Name}", $"{i.Item2}", $"{i.Item3}", $"{i.Item4:F3}");
                    }

                    AnsiConsole.Write(table1);
                }
            }
            #endregion

            #region Grand Masters
            //额外显示GM杯信息
            if (@event.IsScenario(ScenarioType.GrandMasters))
            {
                var spiritNames = new Dictionary<int, string>
                {
                    { -1, "⚪" },
                    { 1, "[red]速[/]" },
                    { 2, "[red]耐[/]" },
                    { 3, "[red]力[/]" },
                    { 4, "[red]根[/]" },
                    { 5, "[red]智[/]" },
                    { 6, "[red]星[/]" },
                    { 9, "[blue]速[/]" },
                    { 10,"[blue]耐[/]" },
                    { 11,"[blue]力[/]" },
                    { 12,"[blue]根[/]" },
                    { 13,"[blue]智[/]" },
                    { 14,"[blue]星[/]" },
                    { 17,"[yellow]速[/]" },
                    { 18,"[yellow]耐[/]" },
                    { 19,"[yellow]力[/]" },
                    { 20,"[yellow]根[/]" },
                    { 21,"[yellow]智[/]" },
                    { 22,"[yellow]星[/]" },
                };

                string outputLine = "当前碎片组：";
                int[] spiritColors = new int[8]; //0空，1红，2蓝，3黄
                for (int spiritPlace = 1; spiritPlace < 9; spiritPlace++)
                {
                    int spiritId =
                        @event.data.venus_data_set.spirit_info_array.Any(x => x.spirit_num == spiritPlace)
                        ? @event.data.venus_data_set.spirit_info_array.First(x => x.spirit_num == spiritPlace).spirit_id
                        : -1;
                    spiritColors[spiritPlace - 1] = (8 + spiritId) / 8;  //0空，1红，2蓝，3黄
                    string spiritStr = spiritNames[spiritId];

                    if (spiritPlace == 1 || spiritPlace == 5)
                        spiritStr = $"{{{spiritStr}}} ";
                    else
                        spiritStr = $"{spiritStr} ";
                    outputLine = outputLine + spiritStr;
                }
                AnsiConsole.MarkupLine(outputLine);

                //看看有没有凑齐的女神
                if (@event.data.venus_data_set.spirit_info_array.Any(x => x.spirit_id == 9040)) AnsiConsole.MarkupLine("当前女神睿智：[red]红[/]");
                else if (@event.data.venus_data_set.spirit_info_array.Any(x => x.spirit_id == 9041)) AnsiConsole.MarkupLine("当前女神睿智：[blue]蓝[/]");
                else if (@event.data.venus_data_set.spirit_info_array.Any(x => x.spirit_id == 9042)) AnsiConsole.MarkupLine("当前女神睿智：[yellow]黄[/]");
                else //预测下一个女神
                {
                    string[] colorStrs = { "⚪", "[red]红[/]", "[blue]蓝[/]", "[yellow]黄[/]" };
                    if (spiritColors[0] == 0)
                        AnsiConsole.MarkupLine("下一个女神：⚪ [green]vs[/] ⚪");
                    else if (spiritColors[0] != 0 && spiritColors[4] == 0)
                    {
                        int color1 = spiritColors[0];
                        int color1count = spiritColors.Count(x => x == color1);
                        AnsiConsole.MarkupLine($"下一个女神：{colorStrs[color1]}x{color1count} [green]vs[/] ⚪");
                    }
                    else
                    {
                        int color1 = spiritColors[0];
                        int color1count = spiritColors.Count(x => x == color1);
                        int color2 = spiritColors[4];
                        int color2count = spiritColors.Count(x => x == color2);
                        int emptycount = spiritColors.Count(x => x == 0);
                        if (color1 == color2 || color1count > color2count + emptycount)
                            AnsiConsole.MarkupLine($"下一个女神：{colorStrs[color1]}");
                        else if (color2count > color1count + emptycount)
                            AnsiConsole.MarkupLine($"下一个女神：{colorStrs[color2]}");
                        else
                            AnsiConsole.MarkupLine($"下一个女神：{colorStrs[color1]}x{color1count} [green]vs[/] {colorStrs[color2]}x{color2count}");
                    }
                }

                if (@event.data.venus_data_set.venus_chara_info_array != null && @event.data.venus_data_set.venus_chara_info_array.Any(x => x.chara_id == 9042))
                {
                    var venusLevels = @event.data.venus_data_set.venus_chara_info_array;
                    turnStat.venus_yellowVenusLevel = venusLevels.First(x => x.chara_id == 9042).venus_level;
                    turnStat.venus_redVenusLevel = venusLevels.First(x => x.chara_id == 9040).venus_level;
                    turnStat.venus_blueVenusLevel = venusLevels.First(x => x.chara_id == 9041).venus_level;
                    AnsiConsole.MarkupLine($"女神等级：" +
                        $"[yellow]{turnStat.venus_yellowVenusLevel}[/] " +
                        $"[red]{turnStat.venus_redVenusLevel}[/] " +
                        $"[blue]{turnStat.venus_blueVenusLevel}[/] "
                        );
                }
                bool isBlueActive = @event.data.venus_data_set.venus_spirit_active_effect_info_array.Any(x => x.chara_id == 9041);//是否开蓝了
                if (isBlueActive)
                    turnStat.venus_isVenusCountConcerned = false;

            }
            //女神情热状态，不统计女神召唤次数
            if (@event.data.chara_info.chara_effect_id_array.Any(x => x == 102))
            {
                turnStat.venus_isVenusCountConcerned = false;
                turnStat.venus_isEffect102 = true;
                //统计一下女神情热持续了几回合
                int continuousTurnNum = 0;
                for (int i = turnNum; i >= 1; i--)
                {
                    if (GameStats.stats[i] == null || !GameStats.stats[i].venus_isEffect102)
                        break;
                    continuousTurnNum++;
                }
                AnsiConsole.MarkupLine($"女神彩圈已持续[green]{continuousTurnNum}[/]回合");
            }
            #endregion

            var trainItems = new Dictionary<int, SingleModeCommandInfo>();
            if (@event.IsScenario(ScenarioType.LArc))
            {
                //LArc的合宿ID不一样，所以要单独处理
                trainItems.Add(101, @event.data.home_info.command_info_array.Any(x => x.command_id == 1101) ? @event.data.home_info.command_info_array.First(x => x.command_id == 1101) : @event.data.home_info.command_info_array.First(x => x.command_id == 101));
                trainItems.Add(105, @event.data.home_info.command_info_array.Any(x => x.command_id == 1102) ? @event.data.home_info.command_info_array.First(x => x.command_id == 1102) : @event.data.home_info.command_info_array.First(x => x.command_id == 105));
                trainItems.Add(102, @event.data.home_info.command_info_array.Any(x => x.command_id == 1103) ? @event.data.home_info.command_info_array.First(x => x.command_id == 1103) : @event.data.home_info.command_info_array.First(x => x.command_id == 102));
                trainItems.Add(103, @event.data.home_info.command_info_array.Any(x => x.command_id == 1104) ? @event.data.home_info.command_info_array.First(x => x.command_id == 1104) : @event.data.home_info.command_info_array.First(x => x.command_id == 103));
                trainItems.Add(106, @event.data.home_info.command_info_array.Any(x => x.command_id == 1105) ? @event.data.home_info.command_info_array.First(x => x.command_id == 1105) : @event.data.home_info.command_info_array.First(x => x.command_id == 106));
            }
            else
            {
                //速耐力根智，6xx为合宿时ID
                trainItems.Add(101, @event.data.home_info.command_info_array.Any(x => x.command_id == 601) ? @event.data.home_info.command_info_array.First(x => x.command_id == 601) : @event.data.home_info.command_info_array.First(x => x.command_id == 101));
                trainItems.Add(105, @event.data.home_info.command_info_array.Any(x => x.command_id == 602) ? @event.data.home_info.command_info_array.First(x => x.command_id == 602) : @event.data.home_info.command_info_array.First(x => x.command_id == 105));
                trainItems.Add(102, @event.data.home_info.command_info_array.Any(x => x.command_id == 603) ? @event.data.home_info.command_info_array.First(x => x.command_id == 603) : @event.data.home_info.command_info_array.First(x => x.command_id == 102));
                trainItems.Add(103, @event.data.home_info.command_info_array.Any(x => x.command_id == 604) ? @event.data.home_info.command_info_array.First(x => x.command_id == 604) : @event.data.home_info.command_info_array.First(x => x.command_id == 103));
                trainItems.Add(106, @event.data.home_info.command_info_array.Any(x => x.command_id == 605) ? @event.data.home_info.command_info_array.First(x => x.command_id == 605) : @event.data.home_info.command_info_array.First(x => x.command_id == 106));
            }

            var trainStats = new TrainStats[5];
            var failureRate = new Dictionary<int, int>();
            for (int t = 0; t < 5; t++)
            {
                int tid = GameGlobal.TrainIds[t];
                failureRate[tid] = trainItems[tid].failure_rate;
                var trainParams = new Dictionary<int, int>()
                {
                    {1,0},
                    {2,0},
                    {3,0},
                    {4,0},
                    {5,0},
                    {30,0},
                    {10,0},
                };
                var nonScenarioTrainParams = new Dictionary<int, int>()
                {
                    {1,0},
                    {2,0},
                    {3,0},
                    {4,0},
                    {5,0},
                    {30,0},
                    {10,0},
                };
                //去掉剧本加成的训练值（游戏里的下层显示）
                foreach (var item in @event.data.home_info.command_info_array)
                {
                    if (GameGlobal.ToTrainId.ContainsKey(item.command_id) &&
                        GameGlobal.ToTrainId[item.command_id] == tid)
                    {
                        foreach (var trainParam in item.params_inc_dec_info_array)
                        {
                            nonScenarioTrainParams[trainParam.target_type] += trainParam.value;
                            trainParams[trainParam.target_type] += trainParam.value;
                        }
                    }
                }

                //青春杯
                if (@event.data.team_data_set != null)
                {
                    foreach (var item in @event.data.team_data_set.command_info_array)
                    {
                        if (item.command_id == tid || item.command_id == GameGlobal.XiahesuIds[tid])
                        {
                            foreach (var trainParam in item.params_inc_dec_info_array)
                            {
                                trainParams[trainParam.target_type] += trainParam.value;
                                //AnsiConsole.WriteLine($"{tid} {trainParam.target_type} {trainParam.value}");
                            }
                        }
                    }
                }
                //巅峰杯
                if (@event.data.free_data_set != null)
                {
                    foreach (var item in @event.data.free_data_set.command_info_array)
                    {
                        if (item.command_id == tid || item.command_id == GameGlobal.XiahesuIds[tid])
                        {
                            foreach (var trainParam in item.params_inc_dec_info_array)
                            {
                                trainParams[trainParam.target_type] += trainParam.value;
                                //AnsiConsole.WriteLine($"{tid} {trainParam.target_type} {trainParam.value}");
                            }
                        }
                    }
                }
                //偶像杯
                if (@event.data.live_data_set != null)
                {
                    foreach (var item in @event.data.live_data_set.command_info_array)
                    {
                        if (item.command_id == tid || item.command_id == GameGlobal.XiahesuIds[tid])
                        {
                            foreach (var trainParam in item.params_inc_dec_info_array)
                            {
                                trainParams[trainParam.target_type] += trainParam.value;
                                //AnsiConsole.WriteLine($"{tid} {trainParam.target_type} {trainParam.value}");
                            }
                        }
                    }
                }
                //女神杯
                if (@event.IsScenario(ScenarioType.GrandMasters))
                {
                    foreach (var item in @event.data.venus_data_set.command_info_array)
                    {
                        if (item.command_id == tid || item.command_id == GameGlobal.XiahesuIds[tid])
                        {
                            foreach (var trainParam in item.params_inc_dec_info_array)
                            {
                                trainParams[trainParam.target_type] += trainParam.value;
                            }
                        }
                    }
                }

                //凯旋门
                if (@event.IsScenario(ScenarioType.LArc))
                {
                    foreach (var item in @event.data.arc_data_set.command_info_array)
                    {
                        if (GameGlobal.ToTrainId.ContainsKey(item.command_id) &&
                            GameGlobal.ToTrainId[item.command_id] == tid)
                        {
                            foreach (var trainParam in item.params_inc_dec_info_array)
                            {
                                trainParams[trainParam.target_type] += trainParam.value;
                            }
                        }
                    }
                }

                var stats = new TrainStats();
                stats.FailureRate = trainItems[tid].failure_rate;
                stats.VitalGain = trainParams[10];
                if (currentVital + stats.VitalGain > maxVital)
                    stats.VitalGain = maxVital - currentVital;
                if (stats.VitalGain < -currentVital)
                    stats.VitalGain = -currentVital;
                stats.FiveValueGain = new int[] { trainParams[1], trainParams[2], trainParams[3], trainParams[4], trainParams[5] };
                for (int i = 0; i < 5; i++)
                    stats.FiveValueGain[i] = ScoreUtils.ReviseOver1200(currentFiveValue[i] + stats.FiveValueGain[i]) - ScoreUtils.ReviseOver1200(currentFiveValue[i]);
                stats.PtGain = trainParams[30];
                stats.FiveValueGainNonScenario = new int[] { nonScenarioTrainParams[1], nonScenarioTrainParams[2], nonScenarioTrainParams[3], nonScenarioTrainParams[4], nonScenarioTrainParams[5] };
                for (int i = 0; i < 5; i++)
                    stats.FiveValueGainNonScenario[i] = ScoreUtils.ReviseOver1200(currentFiveValue[i] + stats.FiveValueGainNonScenario[i]) - ScoreUtils.ReviseOver1200(currentFiveValue[i]);
                stats.PtGainNonScenario = nonScenarioTrainParams[30];
                trainStats[t] = stats;
            }
            turnStat.fiveTrainStats = trainStats;
            var table = new Table();

            var failureRateStr = new string[5];
            //失败率>=40%标红、>=20%(有可能大失败)标DarkOrange、>0%标黄
            for (int i = 0; i < 5; i++)
            {
                int thisFailureRate = failureRate[GameGlobal.TrainIds[i]];
                string outputItem = $"({thisFailureRate}%)";
                if (thisFailureRate >= 40)
                    outputItem = "[red]" + outputItem + "[/]";
                else if (thisFailureRate >= 20)
                    outputItem = "[darkorange]" + outputItem + "[/]";
                else if (thisFailureRate > 0)
                    outputItem = "[yellow]" + outputItem + "[/]";
                failureRateStr[i] = outputItem;
            }
            int tableWidth = 15;
            table.AddColumns(
                  new TableColumn($"速{failureRateStr[0]}").Width(tableWidth)
                , new TableColumn($"耐{failureRateStr[1]}").Width(tableWidth)
                , new TableColumn($"力{failureRateStr[2]}").Width(tableWidth)
                , new TableColumn($"根{failureRateStr[3]}").Width(tableWidth)
                , new TableColumn($"智{failureRateStr[4]}").Width(tableWidth));
            if (@event.IsScenario(ScenarioType.LArc))
            {
                table.AddColumn(new TableColumn("SS Match").Width(tableWidth));
            }
            var separatorLine = Enumerable.Repeat(new string(Enumerable.Repeat('-', table.Columns.Max(x => x.Width.GetValueOrDefault())).ToArray()), 5).ToArray();
            var separatorLineSSMatch = new string(Enumerable.Repeat('-', tableWidth).ToArray());
            var outputItems = new string[5];

            //table.AddRow(Enumerable.Repeat("当前:可获得", 5).ToArray());
            table.AddToRows(0, Enumerable.Repeat("当前:可获得", 5).ToArray());
            //显示此属性的当前属性及还差多少属性达到上限
            for (int i = 0; i < 5; i++)
            {
                int remainValue = fiveValueMaxRevised[i] - currentFiveValueRevised[i];
                outputItems[i] = $"{currentFiveValueRevised[i]}:";
                if (remainValue > 400)
                    outputItems[i] += $"{remainValue}属性";
                else if (remainValue > 200)
                    outputItems[i] += $"[yellow]{remainValue}[/]属性";
                else
                    outputItems[i] += $"[red]{remainValue}[/]属性";
            }
            //table.AddRow(outputItems);
            //table.AddRow(separatorLine);
            table.AddToRows(1, outputItems);
            table.AddToRows(2, separatorLine);

            //显示训练后的剩余体力
            for (int i = 0; i < 5; i++)
            {
                int tid = GameGlobal.TrainIds[i];
                var VitalGain = trainStats[i].VitalGain;
                var newVital = VitalGain + currentVital;

                string outputItem;
                if (newVital < 30)
                    outputItem = $"体力:[red]{newVital}[/]/{maxVital}";
                else if (newVital < 50)
                    outputItem = $"体力:[darkorange]{newVital}[/]/{maxVital}";
                else if (newVital < 70)
                    outputItem = $"体力:[yellow]{newVital}[/]/{maxVital}";
                else
                    outputItem = $"体力:[green]{newVital}[/]/{maxVital}";

                outputItems[i] = outputItem;
            }
            //table.AddRow(outputItems);
            table.AddToRows(3, outputItems);

            //显示此训练的训练等级
            for (int i = 0; i < 5; i++)
            {
                int normalId = GameGlobal.TrainIds[i];
                if (@event.data.home_info.command_info_array.Any(x => x.command_id == GameGlobal.XiahesuIds[normalId]))
                {
                    outputItems[i] = "[green]夏合宿[/]";
                }
                else if (@event.IsScenario(ScenarioType.LArc) && LArcIsAbroad)
                {
                    outputItems[i] = "[green]远征[/]";
                }
                else
                {
                    var lv = @event.data.chara_info.training_level_info_array.First(x => x.command_id == normalId).level;
                    if (@event.IsScenario(ScenarioType.LArc) && turnStat.trainLevel[i] != lv && !isRepeat)
                    {
                        //可能是半途开启小黑板，也可能是有未知bug
                        AnsiConsole.MarkupLine($"[red]警告：训练等级预测错误，预测{GameGlobal.TrainNames[normalId]}为lv{turnStat.trainLevel[i]}(+{turnStat.trainLevelCount[i]})，实际为lv{lv}[/]");
                        turnStat.trainLevel[i] = lv;
                        turnStat.trainLevelCount[i] = 0;//如果是半途开启小黑板，则会在下一次升级时变成正确的计数
                    }
                    if (@event.IsScenario(ScenarioType.LArc))
                        outputItems[i] = lv < 5 ? $"[yellow]Lv{lv}[/](+{turnStat.trainLevelCount[i]})" : $"Lv{lv}";
                    else
                        outputItems[i] = lv < 5 ? $"[yellow]Lv{lv}[/]" : $"Lv{lv}";
                }
            }
            //table.AddRow(outputItems);
            //table.AddRow(separatorLine);
            table.AddToRows(4, outputItems);
            table.AddToRows(5, separatorLine);

            //显示此次训练可获得的属性和Pt
            int bestScore = -100;
            int bestTrain = -1;
            for (int i = 0; i < 5; i++)
            {
                int tid = GameGlobal.TrainIds[i];
                var stats = trainStats[i];
                int score = stats.FiveValueGain.Sum();
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTrain = i;
                }
                outputItems[i] = $"{score}";
            }
            for (int i = 0; i < 5; i++)
            {
                if (i == bestTrain)
                    outputItems[i] = $"属性:[aqua]{outputItems[i]}[/]|Pt:{trainStats[i].PtGain}";
                else
                    outputItems[i] = $"属性:[green]{outputItems[i]}[/]|Pt:{trainStats[i].PtGain}";
            }
            //table.AddRow(outputItems);
            //table.AddRow(separatorLine);
            table.AddToRows(6, outputItems);

            //显示此次训练可获得的打分
            double bestValue = -100;
            bestTrain = -1;
            for (int i = 0; i < 5; i++)
            {
                int tid = GameGlobal.TrainIds[i];
                var stats = trainStats[i];
                double score = 1.0 * stats.FiveValueGain[0]
                    + 1.1 * stats.FiveValueGain[1]
                    + 1.1 * stats.FiveValueGain[2]
                    + 1.1 * stats.FiveValueGain[3]
                    + 0.9 * stats.FiveValueGain[4]
                    + 0.4 * stats.PtGain;
                if (score > bestValue)
                {
                    bestValue = score;
                    bestTrain = i;
                }
                outputItems[i] = $"{score:F1}";
            }
            for (int i = 0; i < 5; i++)
            {
                if (i == bestTrain)
                    outputItems[i] = $"打分:[aqua]{outputItems[i]}[/]";
                else
                    outputItems[i] = $"打分:[green]{outputItems[i]}[/]"; ;
            }
            //table.AddRow(outputItems);
            //table.AddRow(separatorLine);

            //以下几项用于计算单次训练能充多少格
            var LArcRivalBoostCount = new int[,] { { 0, 0, 0 }, { 0, 0, 0 }, { 0, 0, 0 }, { 0, 0, 0 }, { 0, 0, 0 } };// 五种训练的充电槽为0,1,2格的个数
            var LArcShiningCount = new int[] { 0, 0, 0, 0, 0 };//彩圈个数
            var LArcfriendAppear = new bool[] { false, false, false, false, false };//友人在不在

            // 当前S卡卡组
            var supportCards = @event.data.chara_info.support_card_array.ToDictionary(x => x.position, x => x.support_card_id);
            var commandInfo = new Dictionary<int, string[]>();
            foreach (var command in @event.data.home_info.command_info_array)
            {
                if (!GameGlobal.ToTrainIndex.ContainsKey(command.command_id)) continue;


                var trainIdx = GameGlobal.ToTrainIndex[command.command_id];

                var tips = command.tips_event_partner_array.Intersect(command.training_partner_array); //红感叹号 || Hint
                var partners = command.training_partner_array
                    .Select(partner =>
                    {
                        turnStat.isTraining = true;
                        var priority = PartnerPriority.默认;

                        // partner是当前S卡卡组的index（1~6，7是啥？我忘了）或者charaId（10xx)
                        var name = (partner >= 1 && partner <= 7 ? Database.Names.GetSupportCard(supportCards[partner]).Nickname : Database.Names.GetCharacter(partner).Nickname).EscapeMarkup();
                        var friendship = @event.data.chara_info.evaluation_info_array.First(x => x.target_id == partner).evaluation;
                        bool isArcPartner = @event.IsScenario(ScenarioType.LArc) && (partner > 1000 || (partner >= 1 && partner <= 7)) && @event.data.arc_data_set.evaluation_info_array.Any(x => x.target_id == partner);
                        var nameColor = "[#ffffff]";
                        var nameAppend = "";
                        bool shouldShining = false;//是不是友情训练
                        if (partner >= 1 && partner <= 7)
                        {
                            priority = PartnerPriority.其他;
                            if (name.Contains("[友]")) //友人单独标绿
                            {
                                priority = PartnerPriority.友人;
                                nameColor = $"[green]";

                                //三女神团队卡的友情训练
                                if (supportCards[partner] == 30137)
                                {
                                    turnStat.venus_venusTrain = GameGlobal.ToTrainId[command.command_id];
                                }

                                if (supportCards[partner] == 30160 || supportCards[partner] == 10094)//佐岳友人卡
                                {
                                    LArcfriendAppear[trainIdx] = true;
                                    turnStat.larc_zuoyueAtTrain[trainIdx] = true;
                                }
                            }
                            else if (friendship < 80) //羁绊不满80，无法触发友情训练标黄
                            {
                                priority = PartnerPriority.羁绊不足;
                                nameColor = $"[yellow]";
                            }

                            //闪彩标蓝
                            {
                                //在得意位置上
                                var commandId1 = GameGlobal.ToTrainId[command.command_id];
                                shouldShining = friendship >= 80 &&
                                    name.Contains(commandId1 switch
                                    {
                                        101 => "[速]",
                                        105 => "[耐]",
                                        102 => "[力]",
                                        103 => "[根]",
                                        106 => "[智]",
                                    });
                                //GM杯检查
                                if (@event.IsScenario(ScenarioType.GrandMasters) && @event.data.venus_data_set.venus_spirit_active_effect_info_array.Any(x => x.chara_id == 9042 && x.effect_group_id == 421)
                                    && (name.Contains("[速]") || name.Contains("[耐]") || name.Contains("[力]") || name.Contains("[根]") || name.Contains("[智]")))
                                {
                                    shouldShining = true;
                                }

                                if ((supportCards[partner] == 30137 && @event.data.chara_info.chara_effect_id_array.Any(x => x == 102)) || //神团
                                (supportCards[partner] == 30067 && @event.data.chara_info.chara_effect_id_array.Any(x => x == 101)) || //皇团
                                (supportCards[partner] == 30081 && @event.data.chara_info.chara_effect_id_array.Any(x => x == 100)) //天狼星
                                )
                                {
                                    shouldShining = true;
                                    nameColor = $"[#80ff00]";
                                }
                            }

                            if (shouldShining)
                            {
                                LArcShiningCount[trainIdx] += 1;
                                if (name.Contains("[友]"))
                                {
                                    priority = PartnerPriority.友人;
                                    nameColor = $"[#80ff00]";
                                }
                                else
                                {
                                    priority = PartnerPriority.闪;
                                    nameColor = $"[aqua]";
                                }
                            }
                        }
                        else
                        {
                            if (partner >= 100 && partner < 1000)//理事长、记者等
                            {
                                priority = PartnerPriority.关键NPC;
                                nameColor = $"[#008080]";
                            }
                            else if (isArcPartner) // 凯旋门的其他人
                            {
                                priority = PartnerPriority.无用NPC;
                                nameColor = $"[#a166ff]";
                            }
                        }

                        if ((partner >= 1 && partner <= 7) || (partner >= 100 && partner < 1000))//支援卡，理事长，记者，佐岳
                            if (friendship < 100) //羁绊不满100，显示羁绊
                                nameAppend += $"[red]{friendship}[/]";

                        if (isArcPartner && !LArcIsAbroad)
                        {
                            var chara_id = @event.data.arc_data_set.evaluation_info_array.First(x => x.target_id == partner).chara_id;
                            if (@event.data.arc_data_set.arc_rival_array.Any(x => x.chara_id == chara_id))
                            {
                                var arc_data = @event.data.arc_data_set.arc_rival_array.First(x => x.chara_id == chara_id);
                                var rival_boost = arc_data.rival_boost;
                                var effectId = arc_data.selection_peff_array.First(x => x.effect_num == arc_data.selection_peff_array.Min(x => x.effect_num)).effect_group_id;
                                if (rival_boost != 3)
                                {
                                    if (priority > PartnerPriority.需要充电) priority = PartnerPriority.需要充电;
                                    LArcRivalBoostCount[trainIdx, rival_boost] += 1;

                                    if (partner > 1000)
                                        nameColor = $"[#ff00ff]";
                                    nameAppend += $":[aqua]{rival_boost}[/]{GameGlobal.LArcSSEffectNameColoredShort[effectId]}";
                                }
                            }
                        }

                        name = $"{nameColor}{name}[/]{nameAppend}";
                        name = tips.Contains(partner) ? $"[red]![/]{name}" : name; //有Hint就加个红感叹号，和游戏内表现一样

                        return (priority, name);
                    }).ToArray();

                // 按照优先级排序
                commandInfo.Add(command.command_id, partners.OrderBy(s => s.priority).Select(x => x.name).ToArray());
            }
            if (!commandInfo.SelectMany(x => x.Value).Any()) return;

            //LArc充电槽计数
            if (@event.IsScenario(ScenarioType.LArc) && (!LArcIsAbroad))
            {
                for (int i = 0; i < 5; i++)
                {
                    int chargedNum = LArcRivalBoostCount[i, 0] + LArcRivalBoostCount[i, 1] + LArcRivalBoostCount[i, 2];
                    int chargedFullNum = LArcRivalBoostCount[i, 2];
                    if (LArcShiningCount[i] >= 1)
                    {
                        chargedNum += LArcRivalBoostCount[i, 0] + LArcRivalBoostCount[i, 1];
                        chargedFullNum += LArcRivalBoostCount[i, 1];
                    }
                    if (LArcShiningCount[i] >= 2)
                    {
                        chargedNum += LArcRivalBoostCount[i, 0];
                        chargedFullNum += LArcRivalBoostCount[i, 0];
                    }
                    var maybefriendStr = LArcfriendAppear[i] ? "+友" : "";
                    outputItems[i] = $"格数[#00ff00]{chargedNum}{maybefriendStr}[/]|满数[#00ff00]{chargedFullNum}[/]";
                }

                table.AddToRows(7, outputItems);
            }
            table.AddToRows(8, separatorLine);

            for (var i = 0; i < 5; ++i)
            {
                table.AddToRows(9 + i, commandInfo.Select(x => x.Value.Length > i ? x.Value[i] : string.Empty).ToArray());//第8行预留位置
            }

            if (@event.IsScenario(ScenarioType.MakeANewTrack))
            {
                var freeDataSet = @event.data.free_data_set;
                var coinNum = freeDataSet.coin_num;
                var inventory = freeDataSet.user_item_info_array?.ToDictionary(x => x.item_id, x => x.num);
                var shouldPromoteTea = (inventory?.ContainsKey(2301) ?? false) ||  //包里或者商店里有加干劲的道具
                    (inventory?.ContainsKey(2302) ?? false) ||
                    freeDataSet.pick_up_item_info_array.Any(x => x.item_id == 2301) ||
                    freeDataSet.pick_up_item_info_array.Any(x => x.item_id == 2302);
                var currentTurn = @event.data.chara_info.turn;

                var rows = new List<List<string>> { new(), new(), new(), new(), new() };
                {
                    var k = 0;
                    foreach (var j in freeDataSet.pick_up_item_info_array
                        .Where(x => x.item_buy_num != 1)
                        .GroupBy(x => x.item_id))
                    {
                        if (k == 5) k = 0;
                        var name = Database.ClimaxItem[j.First().item_id];
                        if (name.Contains("+15") ||
                            name.Contains("体力+") ||
                            (name == "苦茶" && shouldPromoteTea) ||
                            name == "BBQ" ||
                            name == "切者" ||
                            name == "哨子" ||
                            name == "60%喇叭" ||
                            name == "御守" ||
                            name == "蹄铁・極"
                            )
                            name = $"[green]{name}[/]";
                        var itemCount = j.Count();
                        var remainTurn = j.First().limit_turn == 0 ? ((currentTurn - 1) / 6 + 1) * 6 + 1 - currentTurn : j.First().limit_turn + 1 - currentTurn;
                        var remainTurnRemind = $"{remainTurn}T";
                        if (remainTurn == 3)
                            remainTurnRemind = $"[green1]{remainTurnRemind}[/]";
                        else if (remainTurn == 2)
                            remainTurnRemind = $"[orange1]{remainTurnRemind}[/]";
                        else if (remainTurn == 1)
                            remainTurnRemind = $"[red]{remainTurnRemind}[/]";
                        rows[k].Add($"{name}:{itemCount}/{remainTurnRemind}");
                        k++;
                    }
                }
                for (var i = 0; i < 5; ++i)
                    table.Columns[i].Footer = new Rows(rows[i].Select(x => new Markup(x)));
            }
            if (@event.IsScenario(ScenarioType.GrandMasters))
            {
                var spiritId = new Dictionary<int, string>
                {
                    { 1, "[red]速[/]" },
                    { 2, "[red]耐[/]" },
                    { 3, "[red]力[/]" },
                    { 4, "[red]根[/]" },
                    { 5, "[red]智[/]" },
                    { 6, "[red]星[/]" },
                    { 9, "[blue]速[/]" },
                    { 10,"[blue]耐[/]" },
                    { 11,"[blue]力[/]" },
                    { 12,"[blue]根[/]" },
                    { 13,"[blue]智[/]" },
                    { 14,"[blue]星[/]" },
                    { 17,"[yellow]速[/]" },
                    { 18,"[yellow]耐[/]" },
                    { 19,"[yellow]力[/]" },
                    { 20,"[yellow]根[/]" },
                    { 21,"[yellow]智[/]" },
                    { 22,"[yellow]星[/]" },
                };
                foreach (var i in @event.data.venus_data_set.venus_chara_command_info_array)
                {
                    switch (i.command_type)
                    {
                        case 1:
                            {
                                switch (i.command_id)
                                {
                                    case 101:
                                    case 601:
                                        table.Columns[0].Header = new Markup($"速{failureRateStr[0]} | {spiritId[i.spirit_id]}{(i.is_boost == 1 ? "[aqua]x2[/]" : string.Empty)}"); break;
                                    case 102:
                                    case 603:
                                        table.Columns[2].Header = new Markup($"力{failureRateStr[2]} | {spiritId[i.spirit_id]}{(i.is_boost == 1 ? "[aqua]x2[/]" : string.Empty)}"); break;
                                    case 103:
                                    case 604:
                                        table.Columns[3].Header = new Markup($"根{failureRateStr[3]} | {spiritId[i.spirit_id]}{(i.is_boost == 1 ? "[aqua]x2[/]" : string.Empty)}"); break;
                                    case 105:
                                    case 602:
                                        table.Columns[1].Header = new Markup($"耐{failureRateStr[1]} | {spiritId[i.spirit_id]}{(i.is_boost == 1 ? "[aqua]x2[/]" : string.Empty)}"); break;
                                    case 106:
                                    case 605:
                                        table.Columns[4].Header = new Markup($"智{failureRateStr[4]} | {spiritId[i.spirit_id]}{(i.is_boost == 1 ? "[aqua]x2[/]" : string.Empty)}"); break;
                                }
                                break;
                            }
                        case 3:
                            {
                                table.Columns[0].Footer = new Rows(new Markup($"出行 | {spiritId[i.spirit_id]}{(i.is_boost == 1 ? "[aqua]x2[/]" : string.Empty)}"));
                                break;
                            }
                        case 4:
                            {
                                table.Columns[2].Footer = new Rows(new Markup($"比赛 | {spiritId[i.spirit_id]}{(i.is_boost == 1 ? "[aqua]x2[/]" : string.Empty)}"));
                                break;
                            }
                        case 7:
                            {
                                table.Columns[1].Footer = new Rows(new Markup($"休息 | {spiritId[i.spirit_id]}{(i.is_boost == 1 ? "[aqua]x2[/]" : string.Empty)}"));
                                break;
                            }
                    }
                }
            }
            if (@event.IsScenario(ScenarioType.LArc))
            {

                int rivalNum = @event.data.arc_data_set.selection_info.selection_rival_info_array.Length;
                turnStat.larc_SSPersonCount = rivalNum;
                for (var i = 0; i < rivalNum; i++)
                {
                    var chara_id = @event.data.arc_data_set.selection_info.selection_rival_info_array[i].chara_id;
                    var rivalName = Database.Names.GetCharacter(chara_id).Nickname;
                    if (rivalNum == 5)
                    {
                        // SS Match中的S卡
                        var sc = supportCards.Values.FirstOrDefault(sc => chara_id == Database.Names.GetSupportCard(sc).CharaId);
                        if (@event.data.arc_data_set.selection_info.selection_rival_info_array[i].mark != 1)
                            rivalName = $"[#ff0000]{rivalName}(可能失败)[/]";
                        // 羁绊不满80的S卡
                        else if (sc != default && @event.data.chara_info.evaluation_info_array[supportCards.First(x => x.Value == sc).Key - 1].evaluation < 80)
                        {
                            rivalName = $"[yellow]{rivalName}[/]";
                        }
                        // SSS Match
                        else if (@event.data.arc_data_set.selection_info.is_special_match == 1)
                            rivalName = $"[#00ffff]{rivalName}[/]";
                        else
                            rivalName = $"[#00ff00]{rivalName}[/]";
                    }

                    var arc_data = @event.data.arc_data_set.arc_rival_array.First(x => x.chara_id == chara_id);
                    var effectId = arc_data.selection_peff_array.First(x => x.effect_num == arc_data.selection_peff_array.Min(x => x.effect_num)).effect_group_id;
                    rivalName += $"({GameGlobal.LArcSSEffectNameColored[effectId]})";
                    table.Edit(5, i, rivalName);
                }

                if (rivalNum == 5)//把攒满但没进ss的人头也显示在下面
                {
                    table.Edit(5, 5, separatorLineSSMatch);
                    table.Edit(5, 6, "[#ffff00]其他满格人头:[/]");

                    int extraHeadCount = 0;

                    foreach (var rival in @event.data.arc_data_set.arc_rival_array)
                    {
                        if (rival.selection_peff_array == null)//马娘自身
                            continue;
                        var chara_id = rival.chara_id;
                        var rival_boost = rival.rival_boost;
                        if (rival_boost != 3)//没攒满
                            continue;
                        if (@event.data.arc_data_set.selection_info.selection_rival_info_array.Any(x => x.chara_id == chara_id))//已经在ss训练中了
                            continue;

                        extraHeadCount += 1;
                        if (extraHeadCount > 5) //只能放得下5个人
                            continue;

                        var rivalName = Database.Names.GetCharacter(chara_id).Nickname;

                        var effectId = rival.selection_peff_array.First(x => x.effect_num == rival.selection_peff_array.Min(x => x.effect_num)).effect_group_id;
                        rivalName += $"({GameGlobal.LArcSSEffectNameColored[effectId]})";
                        table.Edit(5, extraHeadCount + 6, rivalName);
                    }
                    if (extraHeadCount > 5)//有没显示的
                        table.Edit(5, 12, $"[#ffff00]... + {extraHeadCount - 5} 人[/]");

                    turnStat.larc_isSSS = @event.data.arc_data_set.selection_info.is_special_match == 1;


                    //table.Edit(5, rivalNum + 1, $"全胜奖励: {@event.data.arc_data_set.selection_info.all_win_approval_point}");
                }

                // 增加当前SS训练属性和PT的显示
                if (rivalNum > 0)
                {
                    int totalStats = 0;
                    int totalPt = 0;
                    int totalVital = 0;
                    foreach (var item in @event.data.arc_data_set.selection_info.params_inc_dec_info_array)
                    {
                        if (item.target_type >= 1 && item.target_type <= 5)
                            totalStats += item.value;
                        else if (item.target_type == 30)
                            totalPt += item.value;
                        else if (item.target_type == 10)
                            totalVital += item.value;
                        else
                            AnsiConsole.MarkupLine($"{item.target_type} = {item.value}");
                    }
                    foreach (var item in @event.data.arc_data_set.selection_info.bonus_params_inc_dec_info_array)
                    {
                        if (item.target_type >= 1 && item.target_type <= 5)
                            totalStats += item.value;
                        else if (item.target_type == 30)
                            totalPt += item.value;
                        else if (item.target_type == 10)
                            totalVital += item.value;
                        else
                            AnsiConsole.MarkupLine($"{item.target_type} = {item.value}");
                    }
                    string line = $"[#00ffff]属性:{totalStats}|Pt:{totalPt}[/]";

                    table.Edit(5, 13, line);
                }
            }
            table.Finish();
            AnsiConsole.Write(table);

            //远征/没买友情+20%或者pt+10警告
            if (@event.IsScenario(ScenarioType.LArc))
            {
                //两次远征分别是37,60回合
                if (turnNum < 37 && turnNum >= 34)
                    AnsiConsole.MarkupLine($@"[#ff0000]还有{37 - turnNum}回合第二年远征！[/]");
                if (turnNum < 60 && turnNum >= 55)
                    AnsiConsole.MarkupLine($@"[#ff0000]还有{60 - turnNum}回合第三年远征！[/]");
                if (turnNum == 59)
                {
                    AnsiConsole.MarkupLine($@"[#00ffff]下回合第三年远征！[/]");
                    AnsiConsole.MarkupLine($@"[#00ffff]下回合第三年远征！[/]");
                    AnsiConsole.MarkupLine($@"[#00ffff]下回合第三年远征！（重要的事情说三遍）[/]");
                }

                if (turnNum > 42)
                {
                    //十个升级的id分别是
                    //  2 5
                    // 1 4 6
                    // 3 7 8
                    //  9 10
                    //检查是否买了友情+20
                    int friendLevel = @event.data.arc_data_set.arc_info.potential_array.First(x => x.potential_id == 8).level;
                    int ptLevel = @event.data.arc_data_set.arc_info.potential_array.First(x => x.potential_id == 3).level;
                    if (friendLevel < 3)//没买友情
                    {
                        int cost = friendLevel == 2 ? 300 : 500;
                        if (@event.data.arc_data_set.arc_info.global_exp >= cost)
                        {
                            AnsiConsole.MarkupLine($@"[#00ffff]没买友情+20%！[/]");
                            AnsiConsole.MarkupLine($@"[#00ffff]没买友情+20%！[/]");
                            AnsiConsole.MarkupLine($@"[#00ffff]没买友情+20%！（重要的事情说三遍）[/]");
                        }
                    }
                    else if (ptLevel < 3)//买了友情但没买pt+10
                    {
                        int cost = ptLevel == 2 ? 200 : 400;
                        if (@event.data.arc_data_set.arc_info.global_exp >= cost)
                        {
                            AnsiConsole.MarkupLine($@"[#00ffff]没买pt+10！[/]");
                            AnsiConsole.MarkupLine($@"[#00ffff]没买pt+10！[/]");
                            AnsiConsole.MarkupLine($@"[#00ffff]没买pt+10！（重要的事情说三遍）[/]");
                        }
                    }
                    // 增加大逃日本杯提示
                    if (turnNum == 46)
                        AnsiConsole.MarkupLine($@"[#ffff00]日本杯！拿大逃别忘了打！[/]");
                }
            }

            //把当前游戏状态写入一个文件，用于与ai通信。放到最后以收集GameStats数据
#if WRITE_AI
            if (@event.IsScenario(ScenarioType.LArc))
            {
                var gameStatusToSend = new GameStatusSend_LArc(@event);
                SubscribeAiInfo.Signal(gameStatusToSend);
            }
#endif
#if WRITE_GAME_STATISTICS
            if(shouldWriteStatistics)
            {
                GameStats.LArcWriteStatsBeforeTrain(@event);
            }
#endif
        }
    }
}
