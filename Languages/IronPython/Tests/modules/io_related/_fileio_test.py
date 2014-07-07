#####################################################################################
#
#  Copyright (c) Microsoft Corporation. All rights reserved.
#
# This source code is subject to terms and conditions of the Microsoft Public License. A
# copy of the license can be found in the License.html file at the root of this distribution. If
# you cannot locate the  Microsoft Public License, please send an email to
# ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound
# by the terms of the Microsoft Public License.
#
# You must not remove this notice, or any other, from this software.
#
#
#####################################################################################
'''
Tests for CPython's _fileio module.
'''

#--IMPORTS---------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")

import _fileio
import os

#--GLOBALS---------------------------------------------------------------------
TEMP_READINTO_NAME = "_fileio__FileIO_readinto%d.tmp"

#--HELPERS---------------------------------------------------------------------
def bytesio_helper():
    return (bytes(bytearray(b'')),
            bytes(bytearray(b'a')),
            bytes(bytearray(b'ab')),
            bytes(bytearray(b'abc')),
            bytes(bytearray(b'abcd')),
            bytes(bytearray(b'abcde')),
            bytes(bytearray(b'abcdef')),
            bytes(bytearray(b'abcdefg')),
            bytes(bytearray(b'abcdefgh')),
            bytes(bytearray(b'abcdefghi'))
            )

def fileio_helper():
    bytes_io_list = bytesio_helper()
    file_io_list  = []
    for i in xrange(len(bytes_io_list)):
        f = _fileio._FileIO(TEMP_READINTO_NAME % i, "w")
        f.write(bytes_io_list[i])
        f.close()
        file_io_list.append(_fileio._FileIO(TEMP_READINTO_NAME % i, "r"))
    
    return file_io_list

#--TEST CASES------------------------------------------------------------------
def test__FileIO___class__():
    '''
    TODO
    '''
    pass

def test__FileIO___delattr__():
    '''
    TODO
    '''
    pass

def test__FileIO___doc__():
    '''
    TODO
    '''
    pass

def test__FileIO___format__():
    '''
    TODO
    '''
    pass

def test__FileIO___getattribute__():
    '''
    TODO
    '''
    pass

def test__FileIO___hash__():
    '''
    TODO
    '''
    pass

def test__FileIO___init__():
    '''
    TODO
    '''
    pass

def test__FileIO___new__():
    '''
    TODO
    '''
    pass

def test__FileIO___reduce__():
    '''
    TODO
    '''
    pass

def test__FileIO___reduce_ex__():
    '''
    TODO
    '''
    pass

def test__FileIO___repr__():
    '''
    TODO
    '''
    pass

def test__FileIO___setattr__():
    '''
    TODO
    '''
    pass

def test__FileIO___sizeof__():
    '''
    TODO
    '''
    pass

def test__FileIO___str__():
    '''
    TODO
    '''
    pass

def test__FileIO___subclasshook__():
    '''
    TODO
    '''
    pass

def test__FileIO_close():
    '''
    TODO
    '''
    pass

def test__FileIO_closed():
    '''
    TODO
    '''
    pass

def test__FileIO_closefd():
    '''
    TODO
    '''
    pass

def test__FileIO_fileno():
    '''
    TODO
    '''
    pass

def test__FileIO_isatty():
    '''
    TODO
    '''
    pass

def test__FileIO_mode():
    '''
    TODO
    '''
    pass

def test__FileIO_read():
    '''
    TODO
    '''
    pass

def test__FileIO_readable():
    '''
    TODO
    '''
    pass

def test__FileIO_readall():
    '''
    TODO
    '''
    pass

def test__FileIO_readinto():
    '''
    TODO
    '''
    pass

def test__FileIO_seek():
    '''
    TODO
    '''
    pass

def test__FileIO_seekable():
    '''
    TODO
    '''
    pass

def test__FileIO_tell():
    '''
    TODO
    '''
    pass

def test__FileIO_truncate():
    '''
    TODO
    '''
    pass

def test__FileIO_writable():
    '''
    TODO
    '''
    pass

def test__FileIO_write():
    '''
    TODO
    '''
    pass


def test_coverage():
    '''
    Test holes as found by code coverage runs.  These need to be refactored and
    moved to other functions throughout this module (TODO).
    '''

    #--_fileio._FileIO.readinto(array.array(...))
    import array
    
    readinto_cases = [
                        [('c',),
                         [[],[],[],[],[],[],[],[],[],[]],
                         [0,0,0,0,0,0,0,0,0,0]],
                        [('c',['z']),
                         [['z'],['a'],['a'],['a'],['a'],['a'],['a'],['a'],['a'],['a']],
                         [0,1,1,1,1,1,1,1,1,1]],
                        [('c',['a','z']),
                         [['a','z'],['a','z'],['a','b'],['a','b'],['a','b'],['a','b'],['a','b'],['a','b'],['a','b'],['a','b']],
                         [0,1,2,2,2,2,2,2,2,2]],
                        [('b',),
                         [[],[],[],[],[],[],[],[],[],[]],
                         [0,0,0,0,0,0,0,0,0,0]],
                        [('b',[0]),
                         [[0],[97],[97],[97],[97],[97],[97],[97],[97],[97]],
                         [0,1,1,1,1,1,1,1,1,1]],
                        [('b',[0,-1]),
                         [[0,-1],[97,-1],[97,98],[97,98],[97,98],[97,98],[97,98],[97,98],[97,98],[97,98]],
                         [0,1,2,2,2,2,2,2,2,2]],
                        [('b',[0,1,2]),
                         [[0,1,2],[97,1,2],[97,98,2],[97,98,99],[97,98,99],[97,98,99],[97,98,99],[97,98,99],[97,98,99],[97,98,99]],
                         [0,1,2,3,3,3,3,3,3,3]],
                        [('b',[0,1,2,3,4,5,6]),
                         [[0,1,2,3,4,5,6],[97,1,2,3,4,5,6],[97,98,2,3,4,5,6],[97,98,99,3,4,5,6],[97,98,99,100,4,5,6],[97,98,99,100,101,5,6],[97,98,99,100,101,102,6],[97,98,99,100,101,102,103],[97,98,99,100,101,102,103],[97,98,99,100,101,102,103]],
                         [0,1,2,3,4,5,6,7,7,7]],
                        [('b',[0,1,2,3,4,5,6,7]),
                         [[0,1,2,3,4,5,6,7],[97,1,2,3,4,5,6,7],[97,98,2,3,4,5,6,7],[97,98,99,3,4,5,6,7],[97,98,99,100,4,5,6,7],[97,98,99,100,101,5,6,7],[97,98,99,100,101,102,6,7],[97,98,99,100,101,102,103,7],[97,98,99,100,101,102,103,104],[97,98,99,100,101,102,103,104]],
                         [0,1,2,3,4,5,6,7,8,8]],
                        [('b',[0,1,2,3,4,5,6,7,8]),
                         [[0,1,2,3,4,5,6,7,8],[97,1,2,3,4,5,6,7,8],[97,98,2,3,4,5,6,7,8],[97,98,99,3,4,5,6,7,8],[97,98,99,100,4,5,6,7,8],[97,98,99,100,101,5,6,7,8],[97,98,99,100,101,102,6,7,8],[97,98,99,100,101,102,103,7,8],[97,98,99,100,101,102,103,104,8],[97,98,99,100,101,102,103,104,105]],
                         [0,1,2,3,4,5,6,7,8,9]],
                        [('b',[0,1,2,3,4,5,6,7,8,9]),
                         [[0,1,2,3,4,5,6,7,8,9],[97,1,2,3,4,5,6,7,8,9],[97,98,2,3,4,5,6,7,8,9],[97,98,99,3,4,5,6,7,8,9],[97,98,99,100,4,5,6,7,8,9],[97,98,99,100,101,5,6,7,8,9],[97,98,99,100,101,102,6,7,8,9],[97,98,99,100,101,102,103,7,8,9],[97,98,99,100,101,102,103,104,8,9],[97,98,99,100,101,102,103,104,105,9]],
                         [0,1,2,3,4,5,6,7,8,9]],
                        [('B',),
                         [[],[],[],[],[],[],[],[],[],[]],
                         [0,0,0,0,0,0,0,0,0,0]],
                        [('B',[0,1]),
                         [[0,1],[97,1],[97,98],[97,98],[97,98],[97,98],[97,98],[97,98],[97,98],[97,98]],
                         [0,1,2,2,2,2,2,2,2,2]],
                        [('u',),
                         [[],[],[],[],[],[],[],[],[],[]],
                         [0,0,0,0,0,0,0,0,0,0]],
                        [('u',u''),
                         [[],[],[],[],[],[],[],[],[],[]],
                         [0,0,0,0,0,0,0,0,0,0]],
                        [('h',),
                         [[],[],[],[],[],[],[],[],[],[]],
                         [0,0,0,0,0,0,0,0,0,0]],
                        [('h',[1,2]),
                         [[1,2],[97,2],[25185,2],[25185,99],[25185,25699],[25185,25699],[25185,25699],[25185,25699],[25185,25699],[25185,25699]],
                         [0,1,2,3,4,4,4,4,4,4]],
                        [('H',),
                         [[],[],[],[],[],[],[],[],[],[]],
                         [0,0,0,0,0,0,0,0,0,0]],
                        [('H',[]),
                         [[],[],[],[],[],[],[],[],[],[]],
                         [0,0,0,0,0,0,0,0,0,0]],
                        [('H',[49]),
                         [[49],[97],[25185],[25185],[25185],[25185],[25185],[25185],[25185],[25185]],
                         [0,1,2,2,2,2,2,2,2,2]],
                        [('H',[2,3]),
                         [[2,3],[97,3],[25185,3],[25185,99],[25185,25699],[25185,25699],[25185,25699],[25185,25699],[25185,25699],[25185,25699]],
                         [0,1,2,3,4,4,4,4,4,4]],
                        [('i',),
                         [[],[],[],[],[],[],[],[],[],[]],
                         [0,0,0,0,0,0,0,0,0,0]],
                        [('I',),
                         [[],[],[],[],[],[],[],[],[],[]],
                         [0,0,0,0,0,0,0,0,0,0]],
                        [('l',),
                         [[],[],[],[],[],[],[],[],[],[]],
                         [0,0,0,0,0,0,0,0,0,0]],
                        [('L',),
                         [[],[],[],[],[],[],[],[],[],[]],
                         [0,0,0,0,0,0,0,0,0,0]],
                        [('f',[]),
                         [[],[],[],[],[],[],[],[],[],[]],
                         [0,0,0,0,0,0,0,0,0,0]],
                        [('d',),
                         [[],[],[],[],[],[],[],[],[],[]],
                         [0,0,0,0,0,0,0,0,0,0]],

                        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24303
                        [('u',u'z'),
                         [[u'z'],[u'a'],[u'\u6261'],[u'\u6261'],[u'\u6261'],[u'\u6261'],[u'\u6261'],[u'\u6261'],[u'\u6261'],[u'\u6261']],
                         [0,1,2,2,2,2,2,2,2,2]],
                        [('u',u'az'),
                         [[u'a',u'z'],[u'a',u'z'],[u'\u6261',u'z'],[u'\u6261',u'c'],[u'\u6261',u'\u6463'],[u'\u6261',u'\u6463'],[u'\u6261',u'\u6463'],[u'\u6261',u'\u6463'],[u'\u6261',u'\u6463'],[u'\u6261',u'\u6463']],
                         [0,1,2,3,4,4,4,4,4,4]],
                        [('u',u'*'),
                         [[u'*'],[u'a'],[u'\u6261'],[u'\u6261'],[u'\u6261'],[u'\u6261'],[u'\u6261'],[u'\u6261'],[u'\u6261'],[u'\u6261']],
                         [0,1,2,2,2,2,2,2,2,2]],
    
                        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24316
                        [('h',[-1]),
                         [[-1],[-159],[25185],[25185],[25185],[25185],[25185],[25185],[25185],[25185]],
                         [0,1,2,2,2,2,2,2,2,2]],
    
                        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24316
                        [('h',[1,-99,47]),
                         [[1,-99,47],[97,-99,47],[25185,-99,47],[25185,-157,47],[25185,25699,47],[25185,25699,101],[25185,25699,26213],[25185,25699,26213],[25185,25699,26213],[25185,25699,26213]],
                         [0,1,2,3,4,5,6,6,6,6]],
    
                        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24317
                        [('i',[1,2]),
                         [[1,2],[97,2],[25185,2],[6513249,2],[1684234849,2],[1684234849,101],[1684234849,26213],[1684234849,6776421],[1684234849,1751606885],[1684234849,1751606885]],
                         [0,1,2,3,4,5,6,7,8,8]],
                        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24316
                        [('i',[-1]),
                         [[-1],[-159],[-40351],[-10263967],[1684234849],[1684234849],[1684234849],[1684234849],[1684234849],[1684234849]],
                         [0,1,2,3,4,4,4,4,4,4]],
                        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24316
                        [('i',[1,-99,47]),
                         [[1,-99,47],[97,-99,47],[25185,-99,47],[6513249,-99,47],[1684234849,-99,47],[1684234849,-155,47],[1684234849,-39323,47],[1684234849,-10000795,47],[1684234849,1751606885,47],[1684234849,1751606885,105]],
                         [0,1,2,3,4,5,6,7,8,9]],
    
                        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24317
                        [('I',[1L]),
                         [[1L],[97L],[25185L],[6513249L],[1684234849L],[1684234849L],[1684234849L],[1684234849L],[1684234849L],[1684234849L]],
                         [0,1,2,3,4,4,4,4,4,4]],
                        [('I',[1L,999L,47L]),
                         [[1L,999L,47L],[97L,999L,47L],[25185L,999L,47L],[6513249L,999L,47L],[1684234849L,999L,47L],[1684234849L,869L,47L],[1684234849L,26213L,47L],[1684234849L,6776421L,47L],[1684234849L,1751606885L,47L],[1684234849L,1751606885L,105L]],
                         [0,1,2,3,4,5,6,7,8,9]],
    
                        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24317
                        [('l',[1,2]),
                         [[1,2],[97,2],[25185,2],[6513249,2],[1684234849,2],[1684234849,101],[1684234849,26213],[1684234849,6776421],[1684234849,1751606885],[1684234849,1751606885]],
                         [0,1,2,3,4,5,6,7,8,8]],
                        [('l',[-1]),
                         [[-1],[-159],[-40351],[-10263967],[1684234849],[1684234849],[1684234849],[1684234849],[1684234849],[1684234849]],
                         [0,1,2,3,4,4,4,4,4,4]],
                        [('l',[1,-99,47]),
                         [[1,-99,47],[97,-99,47],[25185,-99,47],[6513249,-99,47],[1684234849,-99,47],[1684234849,-155,47],[1684234849,-39323,47],[1684234849,-10000795,47],[1684234849,1751606885,47],[1684234849,1751606885,105]],
                         [0,1,2,3,4,5,6,7,8,9]],
                        [('l',[1,-99,47,48]),
                         [[1,-99,47,48],[97,-99,47,48],[25185,-99,47,48],[6513249,-99,47,48],[1684234849,-99,47,48],[1684234849,-155,47,48],[1684234849,-39323,47,48],[1684234849,-10000795,47,48],[1684234849,1751606885,47,48],[1684234849,1751606885,105,48]],
                         [0,1,2,3,4,5,6,7,8,9]],
                        [('l',[1,-99,47,48,49]),
                         [[1,-99,47,48,49],[97,-99,47,48,49],[25185,-99,47,48,49],[6513249,-99,47,48,49],[1684234849,-99,47,48,49],[1684234849,-155,47,48,49],[1684234849,-39323,47,48,49],[1684234849,-10000795,47,48,49],[1684234849,1751606885,47,48,49],[1684234849,1751606885,105,48,49]],
                         [0,1,2,3,4,5,6,7,8,9]],
                        
                        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24318
                        [('L',[100000000L]),
                         [[100000000L],[100000097L],[99967585L],[90399329L],[1684234849L],[1684234849L],[1684234849L],[1684234849L],[1684234849L],[1684234849L]],
                         [0,1,2,3,4,4,4,4,4,4]],
                        [('L',[1L,99L,47L]),
                         [[1L,99L,47L],[97L,99L,47L],[25185L,99L,47L],[6513249L,99L,47L],[1684234849L,99L,47L],[1684234849L,101L,47L],[1684234849L,26213L,47L],[1684234849L,6776421L,47L],[1684234849L,1751606885L,47L],[1684234849L,1751606885L,105L]],
                         [0,1,2,3,4,5,6,7,8,9]],
                        
                        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24319
                        [('f',[3.1415926535897931]),
                         [[3.1415927410125732],[3.1415636539459229],[3.1466295719146729],[3.5528795719146729],[1.6777999408082104e+22],[1.6777999408082104e+22],[1.6777999408082104e+22],[1.6777999408082104e+22],[1.6777999408082104e+22],[1.6777999408082104e+22]],
                         [0,1,2,3,4,4,4,4,4,4]],
                        [('f',[1.0,3.1400000000000001,0.997]),
                         [[1.0,3.1400001049041748,0.99699997901916504],[1.0000115633010864,3.1400001049041748,0.99699997901916504],[1.0030022859573364,3.1400001049041748,0.99699997901916504],[0.88821989297866821,3.1400001049041748,0.99699997901916504],[1.6777999408082104e+22,3.1400001049041748,0.99699997901916504],[1.6777999408082104e+22,3.1399776935577393,0.99699997901916504],[1.6777999408082104e+22,3.1312496662139893,0.99699997901916504],[1.6777999408082104e+22,3.6156246662139893,0.99699997901916504],[1.6777999408082104e+22,4.371022013021617e+24,0.99699997901916504],[1.6777999408082104e+22,4.371022013021617e+24,0.99700027704238892]],
                         [0,1,2,3,4,5,6,7,8,9]],
                        
                        #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24319
                        [('d',[3.1415926535897931]),
                         [[3.1415926535897931],[3.1415926535898255],[3.1415926535958509],[3.1415926544980697],[3.1415927737073592],[3.1413066714124374],[3.1749980776624374],[187.19987697039599],[8.5408832230361244e+194],[8.5408832230361244e+194]],
                         [0,1,2,3,4,5,6,7,8,8]],
                        [('d',[1.0,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004]),
                         [[1.0,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004],[1.0000000000000215,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004],[1.0000000000055922,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004],[1.0000000014462318,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004],[1.0000003739752616,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004],[1.0000966950812187,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004],[1.0249990388312187,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004],[0.002856443435217224,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004],[8.5408832230361244e+194,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004],[8.5408832230361244e+194,3.140000000000033,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004]],
                         [0,1,2,3,4,5,6,7,8,9]],
                        [('d',[1.0,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5]),
                         [[1.0,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5],[1.0000000000000215,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5],[1.0000000000055922,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5],[1.0000000014462318,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5],[1.0000003739752616,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5],[1.0000966950812187,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5],[1.0249990388312187,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5],[0.002856443435217224,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5],[8.5408832230361244e+194,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5],[8.5408832230361244e+194,3.140000000000033,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5]],
                         [0,1,2,3,4,5,6,7,8,9]],
                        [('d',[1.0,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5,6.5999999999999996]),
                         [[1.0,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5,6.5999999999999996],[1.0000000000000215,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5,6.5999999999999996],[1.0000000000055922,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5,6.5999999999999996],[1.0000000014462318,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5,6.5999999999999996],[1.0000003739752616,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5,6.5999999999999996],[1.0000966950812187,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5,6.5999999999999996],[1.0249990388312187,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5,6.5999999999999996],[0.002856443435217224,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5,6.5999999999999996],[8.5408832230361244e+194,3.1400000000000001,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5,6.5999999999999996],[8.5408832230361244e+194,3.140000000000033,0.99734343339999998,1.1000000000000001,2.2000000000000002,3.2999999999999998,4.4000000000000004,5.5,6.5999999999999996]],
                         [0,1,2,3,4,5,6,7,8,9]],
                    ]

    #Cases working correctly under IronPython
    for a_params, a_expected, f_expected in readinto_cases:
        f_list = fileio_helper()
        
        for i in xrange(len(f_list)):
            a = array.array(*a_params)
            f = f_list[i]
            
            AreEqual(f.readinto(a),
                     f_expected[i])
            AreEqual(a.tolist(),
                     a_expected[i])
        
        #cleanup
        for f in f_list: 
            f.close()
        for i in xrange(len(f_list)):
            os.remove(TEMP_READINTO_NAME % i)

    
#--MAIN------------------------------------------------------------------------
run_test(__name__)
